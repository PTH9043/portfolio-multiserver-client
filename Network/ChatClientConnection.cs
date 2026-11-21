using System.Net.Sockets;
using ChatClient.Diagnostics;
using ChatClient.Protocol;
using Google.Protobuf;
using Multiplayer.Protocol;

namespace ChatClient.Network;

/// <summary>
/// UI와 완전히 분리된 비동기 TCP 채팅 클라이언트 연결 계층입니다.
/// 스레드 안전 송신 직렬화, 분할·병합 패킷 조립, 안전한 종료를 담당합니다.
/// </summary>
public sealed class ChatClientConnection : IAsyncDisposable, IDisposable
{
    private const string TraceSource = "Network/ChatClientConnection.cs";

    private readonly ulong _traceId = ClientRuntimeTrace.NextObjectId();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly PacketStreamDecoder _decoder = new();
    private readonly object _stateLock = new();

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private int _disconnectSignaled;
    private bool _disposed;

    public ChatClientConnection()
    {
        ClientRuntimeTrace.RecordEvent(TraceEventKind.Create, nameof(ChatClientConnection), nameof(ChatClientConnection), _traceId, TraceSource);
    }

    public bool IsConnected
    {
        get
        {
            lock (_stateLock)
                return _tcpClient is { Connected: true } && _stream is not null;
        }
    }

    public event Action<SCLoginResult>? LoginResultReceived;
    public event Action<SCRegisterResult>? RegisterResultReceived;
    public event Action<SCChat>? ChatReceived;
    public event Action<SCRoomUserCount>? RoomUserCountReceived;
    public event Action<SCRoomUsers>? RoomUsersReceived;
    public event Action<SCRoomList>? RoomListReceived;
    public event Action<SCJoinRoomResult>? JoinRoomResultReceived;
    public event Action<SCPong>? PongReceived;
    public event Action<string>? Disconnected;
    public event Action<string>? ErrorOccurred;

    public Task ConnectAndLoginAsync(string host, int port, string nickname, CancellationToken cancellationToken = default)
    {
        return ConnectAndLoginAsync(host, port, username: string.Empty, password: string.Empty, nickname: nickname, cancellationToken: cancellationToken);
    }

    public async Task ConnectAndLoginAsync(string host, int port, string username, string password, string nickname = "", CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var _ = ClientRuntimeTrace.EnterScope(nameof(ChatClientConnection), nameof(ConnectAndLoginAsync), _traceId, TraceSource);

        await DisconnectAsync(notifyDisconnect: false).ConfigureAwait(false);

        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            string tag = string.IsNullOrEmpty(username) ? $"nickname={nickname}" : $"username={username}";
            ClientRuntimeTrace.Log("INFO", $"Connected to {host}:{port} ({tag})", _traceId);
            NetworkStream stream = client.GetStream();
            var cts = new CancellationTokenSource();

            lock (_stateLock)
            {
                _tcpClient = client;
                _stream = stream;
                _receiveCts = cts;
                _decoder.Reset();
                Interlocked.Exchange(ref _disconnectSignaled, 0);
            }

            ClientRuntimeTrace.RecordEvent(TraceEventKind.Post, nameof(ChatClientConnection), nameof(ReceiveLoopAsync), _traceId, TraceSource);
            _receiveTask = Task.Run(() => ReceiveLoopAsync(stream, cts.Token), CancellationToken.None);

            await SendAsync(ECSPacket.CsLogin, new CSLogin
            {
                Nickname = nickname,
                Username = username,
                Password = password
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ClientRuntimeTrace.Log("ERROR", $"ConnectAndLogin failed ({host}:{port}): {ex.Message}", _traceId);
            client.Dispose();
            await DisconnectAsync(notifyDisconnect: false).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 일회성 TCP 연결로 CS_REGISTER를 전송하고 SC_REGISTER_RESULT를 수신한 뒤 연결을 정리합니다.
    /// </summary>
    public static async Task<SCRegisterResult> RegisterAccountAsync(
        string host, int port, string username, string password, string nickname, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        using NetworkStream stream = client.GetStream();

        byte[] frame = PacketFramer.EncodeFrame(ECSPacket.CsRegister, new CSRegister
        {
            Username = username,
            Password = password,
            Nickname = nickname
        });
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);

        var decoder = new PacketStreamDecoder();
        byte[] recvBuf = new byte[ChatProtocolLimits.PacketBufferSize * 2];
        while (true)
        {
            int n = await stream.ReadAsync(recvBuf, cancellationToken).ConfigureAwait(false);
            if (n == 0)
                throw new IOException("회원가입 응답을 받기 전에 서버 연결이 종료되었습니다.");

            decoder.Append(recvBuf.AsSpan(0, n));
            string? frameError;
            while (decoder.TryPopPacket(out ushort rawType, out byte[] body, out frameError))
            {
                if ((ESCPacket)rawType == ESCPacket.ScRegisterResult)
                    return SCRegisterResult.Parser.ParseFrom(body);
            }
            if (frameError is not null)
                throw new InvalidDataException(frameError);
        }
    }

    public Task SendChatAsync(string text, CancellationToken cancellationToken = default)
    {
        return SendAsync(ECSPacket.CsChat, new CSChat { Text = text }, cancellationToken);
    }

    public Task RequestRoomUsersAsync(uint offset, CancellationToken cancellationToken = default)
    {
        return SendAsync(ECSPacket.CsRoomUsers, new CSRoomUsers { Offset = offset }, cancellationToken);
    }

    public Task RequestRoomListAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(ECSPacket.CsRoomList, new CSRoomList(), cancellationToken);
    }

    public Task JoinRoomAsync(uint roomId, CancellationToken cancellationToken = default)
    {
        return SendAsync(ECSPacket.CsJoinRoom, new CSJoinRoom { RoomId = roomId }, cancellationToken);
    }

    public Task SendPingAsync(ulong clientTimeMs, CancellationToken cancellationToken = default)
    {
        return SendAsync(ECSPacket.CsPing, new CSPing { ClientTimeMs = clientTimeMs }, cancellationToken);
    }

    public async Task SendAsync(ECSPacket packetType, IMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var _ = ClientRuntimeTrace.EnterScope(nameof(ChatClientConnection), $"SendAsync({packetType})", _traceId, TraceSource);

        byte[] frame = PacketFramer.EncodeFrame(packetType, message);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            NetworkStream? stream;
            lock (_stateLock)
                stream = _stream;

            if (stream is null)
                throw new InvalidOperationException("서버에 연결되어 있지 않습니다.");

            await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try { _sendLock.Release(); }
            catch (ObjectDisposedException) { }
        }
    }

    public Task DisconnectAsync() => DisconnectAsync(notifyDisconnect: true);

    private async Task DisconnectAsync(bool notifyDisconnect, string reason = "연결을 종료했습니다.")
    {
        TcpClient? client;
        NetworkStream? stream;
        CancellationTokenSource? cts;
        Task? receiveTask;

        lock (_stateLock)
        {
            client = _tcpClient;
            stream = _stream;
            cts = _receiveCts;
            receiveTask = _receiveTask;

            _tcpClient = null;
            _stream = null;
            _receiveCts = null;
            _receiveTask = null;
        }

        if (cts is not null)
        {
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        try { stream?.Close(); }
        catch { }

        try { client?.Close(); }
        catch { }

        if (receiveTask is not null && Task.CurrentId != receiveTask.Id)
        {
            try { await receiveTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch { }
        }

        cts?.Dispose();
        stream?.Dispose();
        client?.Dispose();

        if (notifyDisconnect && client is not null)
            RaiseDisconnectedOnce(reason);
    }

    private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        using var traceScope = ClientRuntimeTrace.EnterScope(nameof(ChatClientConnection), nameof(ReceiveLoopAsync), _traceId, TraceSource, TraceEventKind.Callback);
        byte[] recvBuffer = new byte[ChatProtocolLimits.PacketBufferSize * 4];
        string disconnectReason = "서버 연결이 종료되었습니다.";

        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(recvBuffer, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    disconnectReason = "서버가 연결을 닫았습니다.";
                    break;
                }

                _decoder.Append(recvBuffer.AsSpan(0, bytesRead));

                string? frameError;
                while (_decoder.TryPopPacket(out ushort rawType, out byte[] body, out frameError))
                {
                    DispatchPacket(rawType, body);
                }

                // TryPopPacket이 프로토콜 오류를 보고한 경우 즉시 종료합니다.
                if (frameError is not null)
                {
                    ClientRuntimeTrace.Log("WARNING", frameError, _traceId);
                    ErrorOccurred?.Invoke(frameError);
                    disconnectReason = frameError;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (IOException ex) when (ct.IsCancellationRequested)
        {
            _ = ex;
            return;
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            ClientRuntimeTrace.Log("ERROR", $"ReceiveLoop error: {ex.Message}", _traceId);
            ErrorOccurred?.Invoke($"수신 오류: {ex.Message}");
            disconnectReason = $"연결 끊김: {ex.Message}";
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                CloseTransportOnly();
                RaiseDisconnectedOnce(disconnectReason);
            }
        }
    }

    private void DispatchPacket(ushort rawType, byte[] body)
    {
        using var _ = ClientRuntimeTrace.EnterScope(nameof(ChatClientConnection), $"DispatchPacket({(ESCPacket)rawType})", _traceId, TraceSource);
        try
        {
            switch ((ESCPacket)rawType)
            {
                case ESCPacket.ScLoginResult:
                    LoginResultReceived?.Invoke(SCLoginResult.Parser.ParseFrom(body));
                    break;

                case ESCPacket.ScRegisterResult:
                    RegisterResultReceived?.Invoke(SCRegisterResult.Parser.ParseFrom(body));
                    break;

                case ESCPacket.ScChat:
                    ChatReceived?.Invoke(SCChat.Parser.ParseFrom(body));
                    break;

                case ESCPacket.ScRoomUserCount:
                    RoomUserCountReceived?.Invoke(SCRoomUserCount.Parser.ParseFrom(body));
                    break;

                case ESCPacket.ScRoomUsers:
                    RoomUsersReceived?.Invoke(SCRoomUsers.Parser.ParseFrom(body));
                    break;

                case ESCPacket.ScRoomList:
                    RoomListReceived?.Invoke(SCRoomList.Parser.ParseFrom(body));
                    break;

                case ESCPacket.ScJoinRoomResult:
                    JoinRoomResultReceived?.Invoke(SCJoinRoomResult.Parser.ParseFrom(body));
                    break;

                case ESCPacket.ScPong:
                    PongReceived?.Invoke(SCPong.Parser.ParseFrom(body));
                    break;

                default:
                    ClientRuntimeTrace.Log("WARNING", $"Ignored unknown server packet type={rawType} ({body.Length}B)", _traceId);
                    ErrorOccurred?.Invoke($"알 수 없는 서버 패킷 번호({rawType})를 무시했습니다.");
                    break;
            }
        }
        catch (InvalidProtocolBufferException ex)
        {
            ClientRuntimeTrace.Log("WARNING", $"Packet({rawType}) parse failed: {ex.Message}", _traceId);
            ErrorOccurred?.Invoke($"패킷({rawType}) 파싱 실패: {ex.Message}");
        }
    }

    private void CloseTransportOnly()
    {
        TcpClient? client;
        NetworkStream? stream;
        lock (_stateLock)
        {
            client = _tcpClient;
            stream = _stream;
            _tcpClient = null;
            _stream = null;
        }
        try { stream?.Dispose(); } catch { }
        try { client?.Dispose(); } catch { }
    }

    private void RaiseDisconnectedOnce(string reason)
    {
        if (Interlocked.Exchange(ref _disconnectSignaled, 1) == 0)
            Disconnected?.Invoke(reason);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        ClientRuntimeTrace.RecordEvent(TraceEventKind.Destroy, nameof(ChatClientConnection), nameof(DisposeAsync), _traceId, TraceSource);
        await DisconnectAsync(notifyDisconnect: false).ConfigureAwait(false);
        _sendLock.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ClientRuntimeTrace.RecordEvent(TraceEventKind.Destroy, nameof(ChatClientConnection), nameof(Dispose), _traceId, TraceSource);
        CloseTransportOnly();
        lock (_stateLock)
        {
            try { _receiveCts?.Cancel(); } catch { }
            _receiveCts?.Dispose();
            _receiveCts = null;
        }
        _sendLock.Dispose();
    }
}
