using ChatClient.Diagnostics;
using ChatClient.Network;
using ChatClient.Protocol;
using Multiplayer.Protocol;

namespace ChatClient;

public partial class Form1 : Form
{
    private const string TraceSource = "Form1.cs";

    private readonly ulong _traceId = ClientRuntimeTrace.NextObjectId();
    private readonly ChatClientConnection _connection = new();
    private readonly Font _chatRegularFont;
    private readonly Font _chatBoldFont;
    private bool _loggedIn;
    private bool _busyConnecting;
    private bool _loginRejected;
    private string _myNickname = string.Empty;
    private uint _currentOffset;
    private uint _lastReportedTotal;

    public Form1()
    {
        ClientRuntimeTrace.RecordEvent(TraceEventKind.Create, nameof(Form1), nameof(Form1), _traceId, TraceSource);
        InitializeComponent();
        _chatRegularFont = new Font(_chatLogBox.Font, FontStyle.Regular);
        _chatBoldFont = new Font(_chatLogBox.Font, FontStyle.Bold);

        _connection.LoginResultReceived += packet => MarshalToUi(() => OnLoginResult(packet));
        _connection.ChatReceived += packet => MarshalToUi(() => OnChatReceived(packet));
        _connection.RoomUserCountReceived += packet => MarshalToUi(() => OnRoomUserCount(packet));
        _connection.RoomUsersReceived += packet => MarshalToUi(() => OnRoomUsers(packet));
        _connection.Disconnected += reason => MarshalToUi(() => OnDisconnected(reason));
        _connection.ErrorOccurred += message => MarshalToUi(() => AppendSystemLine($"[오류] {message}", Color.Firebrick));

        _connectButton.Click += async (_, _) => await OnConnectButtonClickAsync();
        _sendButton.Click += async (_, _) => await SendCurrentChatAsync();
        _refreshUsersButton.Click += async (_, _) => await RequestUsersPageAsync(_currentOffset);
        _prevPageButton.Click += async (_, _) =>
        {
            uint prevOffset = _currentOffset >= ChatProtocolLimits.RoomUsersPageSize
                ? _currentOffset - ChatProtocolLimits.RoomUsersPageSize
                : 0;
            await RequestUsersPageAsync(prevOffset);
        };
        _nextPageButton.Click += async (_, _) =>
        {
            uint nextOffset = _currentOffset + ChatProtocolLimits.RoomUsersPageSize;
            await RequestUsersPageAsync(nextOffset);
        };

        _chatInputBox.TextChanged += (_, _) => UpdateChatLengthCounter();
        _chatInputBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                BeginInvoke(async () => await SendCurrentChatAsync());
            }
        };
        _nicknameTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !_connection.IsConnected && !_busyConnecting)
            {
                e.SuppressKeyPress = true;
                BeginInvoke(async () => await OnConnectButtonClickAsync());
            }
        };

        Shown += (_, _) =>
        {
            _nicknameTextBox.Focus();
            AppendSystemLine("상단 닉네임 칸(2~12자 한글·영문·숫자)을 입력하고 [접속 / 로그인]을 누르면 아래 채팅 입력창이 활성화됩니다.", Color.SlateGray);
        };
        FormClosing += (_, _) => _connection.Dispose();
        FormClosed += (_, _) =>
        {
            ClientRuntimeTrace.RecordEvent(TraceEventKind.Destroy, nameof(Form1), "FormClosed", _traceId, TraceSource);
            _chatRegularFont.Dispose();
            _chatBoldFont.Dispose();
        };
        UpdateUiState();
    }

    private async Task OnConnectButtonClickAsync()
    {
        if (_busyConnecting)
            return;

        if (_connection.IsConnected || _loggedIn)
        {
            await _connection.DisconnectAsync();
            return;
        }

        string host = _hostTextBox.Text.Trim();
        int port = (int)_portInput.Value;
        string nickname = _nicknameTextBox.Text.Trim();

        if (string.IsNullOrEmpty(host))
        {
            AppendSystemLine("서버 주소를 입력해 주세요.", Color.DarkOrange);
            return;
        }

        if (!ChatProtocolLimits.IsValidNickname(nickname))
        {
            AppendSystemLine("닉네임은 2~12자의 완성형 한글·영문·숫자만 사용할 수 있습니다.", Color.DarkOrange);
            _nicknameTextBox.Focus();
            return;
        }

        _busyConnecting = true;
        _loginRejected = false;
        _myNickname = nickname;
        _statusLabel.Text = $"상태: {host}:{port} 접속 중...";
        _statusLabel.ForeColor = Color.FromArgb(14, 116, 144);
        UpdateUiState();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _connection.ConnectAndLoginAsync(host, port, nickname, cts.Token);
            AppendSystemLine($"{host}:{port}에 연결했습니다. '{nickname}' 로그인 응답을 기다리는 중...", Color.SteelBlue);
        }
        catch (Exception ex)
        {
            _myNickname = string.Empty;
            _statusLabel.Text = "상태: 접속 실패";
            _statusLabel.ForeColor = Color.Firebrick;
            AppendSystemLine($"서버 접속 실패: {ex.Message}", Color.Firebrick);
        }
        finally
        {
            _busyConnecting = false;
            UpdateUiState();
        }
    }

    private async void OnLoginResult(SCLoginResult packet)
    {
        using var _ = ClientRuntimeTrace.EnterScope(nameof(Form1), nameof(OnLoginResult), _traceId, TraceSource, TraceEventKind.Callback);
        try
        {
            if (packet.Result == ELoginResult.LoginResultSuccess)
            {
                _loggedIn = true;
                _loginRejected = false;
                _currentOffset = 0;
                _lastReportedTotal = packet.UserCount;
                _statusLabel.Text = $"상태: 접속됨 · {_myNickname}";
                _statusLabel.ForeColor = Color.FromArgb(15, 118, 110);
                UpdateUserCountDisplay(packet.UserCount);
                AppendSystemLine($"로그인 성공! 기본 방(#1)에 입장했습니다. (현재 {packet.UserCount:N0}명)", Color.Teal);
                ClientRuntimeTrace.Log("INFO", $"Login success: nickname={_myNickname}, userCount={packet.UserCount}", _traceId);
                UpdateUiState();
                _chatInputBox.Focus();
                await RequestUsersPageAsync(0);
                return;
            }

            string reason = ChatProtocolLimits.DescribeLoginResult(packet.Result);
            ClientRuntimeTrace.Log("WARNING", $"Login rejected: {packet.Result} ({reason})", _traceId);
            AppendSystemLine($"로그인 거절: {reason}", Color.Firebrick);
            _statusLabel.Text = "상태: 로그인 거절";
            _statusLabel.ForeColor = Color.Firebrick;
            _loggedIn = false;
            _loginRejected = true;
            UpdateUiState();
            await _connection.DisconnectAsync();
        }
        catch (Exception ex)
        {
            ClientRuntimeTrace.Log("ERROR", $"OnLoginResult error: {ex.Message}", _traceId);
            AppendSystemLine($"로그인 후처리 오류: {ex.Message}", Color.Firebrick);
        }
    }

    private void OnChatReceived(SCChat packet)
    {
        using var _ = ClientRuntimeTrace.EnterScope(nameof(Form1), nameof(OnChatReceived), _traceId, TraceSource, TraceEventKind.Callback);
        bool isMine = string.Equals(packet.Nickname, _myNickname, StringComparison.Ordinal);
        string timestamp = DateTime.Now.ToString("HH:mm:ss");

        _chatLogBox.SelectionStart = _chatLogBox.TextLength;
        _chatLogBox.SelectionLength = 0;

        _chatLogBox.SelectionColor = Color.FromArgb(100, 116, 139);
        _chatLogBox.SelectionFont = _chatRegularFont;
        _chatLogBox.AppendText($"[{timestamp}] ");

        _chatLogBox.SelectionColor = isMine ? Color.FromArgb(15, 118, 110) : Color.FromArgb(30, 64, 175);
        _chatLogBox.SelectionFont = _chatBoldFont;
        _chatLogBox.AppendText(packet.Nickname);

        _chatLogBox.SelectionColor = Color.FromArgb(15, 23, 42);
        _chatLogBox.SelectionFont = _chatRegularFont;
        _chatLogBox.AppendText($": {packet.Text}{Environment.NewLine}");

        _chatLogBox.ScrollToCaret();
    }

    private void OnRoomUserCount(SCRoomUserCount packet)
    {
        _lastReportedTotal = packet.Count;
        UpdateUserCountDisplay(packet.Count);
        UpdatePagingButtons();
    }

    private void OnRoomUsers(SCRoomUsers packet)
    {
        _currentOffset = packet.Offset;
        _lastReportedTotal = packet.Total;
        UpdateUserCountDisplay(packet.Total);

        _usersListBox.BeginUpdate();
        try
        {
            _usersListBox.Items.Clear();
            for (int i = 0; i < packet.Nicknames.Count; i++)
            {
                uint rank = packet.Offset + (uint)i + 1;
                string name = packet.Nicknames[i];
                string suffix = string.Equals(name, _myNickname, StringComparison.Ordinal) ? " (나)" : string.Empty;
                _usersListBox.Items.Add($"{rank}. {name}{suffix}");
            }
        }
        finally
        {
            _usersListBox.EndUpdate();
        }

        UpdatePagingButtons();
    }

    private void OnDisconnected(string reason)
    {
        bool wasRejected = _loginRejected;
        _loginRejected = false;
        _loggedIn = false;
        _busyConnecting = false;
        _currentOffset = 0;
        _lastReportedTotal = 0;
        _usersListBox.Items.Clear();
        UpdateUserCountDisplay(0);
        if (!wasRejected)
        {
            _statusLabel.Text = "상태: 미연결";
            _statusLabel.ForeColor = Color.FromArgb(71, 85, 105);
            AppendSystemLine(reason, Color.DimGray);
        }
        UpdateUiState();
    }

    private async Task SendCurrentChatAsync()
    {
        using var _ = ClientRuntimeTrace.EnterScope(nameof(Form1), nameof(SendCurrentChatAsync), _traceId, TraceSource);
        if (!_loggedIn || !_connection.IsConnected)
            return;

        string text = _chatInputBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        if (!ChatProtocolLimits.IsValidChatText(text))
        {
            AppendSystemLine($"채팅은 1~{ChatProtocolLimits.ChatMaxChars}자까지만 전송할 수 있습니다.", Color.DarkOrange);
            return;
        }

        try
        {
            _chatInputBox.Clear();
            await _connection.SendChatAsync(text);
        }
        catch (Exception ex)
        {
            AppendSystemLine($"채팅 전송 실패: {ex.Message}", Color.Firebrick);
        }
    }

    private async Task RequestUsersPageAsync(uint offset)
    {
        if (!_loggedIn || !_connection.IsConnected)
            return;

        try
        {
            await _connection.RequestRoomUsersAsync(offset);
        }
        catch (Exception ex)
        {
            AppendSystemLine($"접속자 목록 요청 실패: {ex.Message}", Color.Firebrick);
        }
    }

    private void UpdateUserCountDisplay(uint count)
    {
        _userCountLabel.Text = $"현재 인원: {count:N0} / {ChatProtocolLimits.DefaultRoomMaxUsers:N0}명";
    }

    private void UpdateChatLengthCounter()
    {
        int count = ChatProtocolLimits.CountRunes(_chatInputBox.Text);
        _chatLengthLabel.Text = $"{count} / {ChatProtocolLimits.ChatMaxChars}";
        _chatLengthLabel.ForeColor = count > ChatProtocolLimits.ChatMaxChars
            ? Color.Firebrick
            : Color.FromArgb(100, 116, 139);
    }

    private void UpdatePagingButtons()
    {
        if (!_loggedIn)
        {
            _prevPageButton.Enabled = false;
            _nextPageButton.Enabled = false;
            _pageInfoLabel.Text = "0 / 0쪽";
            return;
        }

        uint pageSize = ChatProtocolLimits.RoomUsersPageSize;
        uint totalPages = _lastReportedTotal == 0 ? 1 : (_lastReportedTotal + pageSize - 1) / pageSize;
        uint currentPage = (_currentOffset / pageSize) + 1;
        if (currentPage > totalPages)
            currentPage = totalPages;

        _pageInfoLabel.Text = $"{currentPage} / {totalPages}쪽";
        _prevPageButton.Enabled = _currentOffset > 0;
        _nextPageButton.Enabled = _currentOffset + pageSize < _lastReportedTotal;
    }

    private void UpdateUiState()
    {
        bool connected = _connection.IsConnected || _loggedIn;
        _hostTextBox.Enabled = !connected && !_busyConnecting;
        _portInput.Enabled = !connected && !_busyConnecting;
        _nicknameTextBox.Enabled = !connected && !_busyConnecting;
        _connectButton.Enabled = !_busyConnecting;
        _connectButton.Text = connected ? "연결 해제" : "접속 / 로그인";

        _chatInputBox.Enabled = _loggedIn;
        _chatInputBox.PlaceholderText = _loggedIn
            ? "메시지를 입력하세요 (최대 200자, Enter로 전송)"
            : "로그인 후 입력 가능 — 먼저 상단 닉네임 입력 후 [접속 / 로그인] 클릭";
        _sendButton.Enabled = _loggedIn;
        _refreshUsersButton.Enabled = _loggedIn;
        UpdatePagingButtons();
        UpdateChatLengthCounter();
    }

    private void AppendSystemLine(string message, Color color)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        _chatLogBox.SelectionStart = _chatLogBox.TextLength;
        _chatLogBox.SelectionLength = 0;
        _chatLogBox.SelectionFont = _chatRegularFont;
        _chatLogBox.SelectionColor = color;
        _chatLogBox.AppendText($"[{timestamp}] * {message}{Environment.NewLine}");
        _chatLogBox.ScrollToCaret();
    }

    private void MarshalToUi(Action action)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            try { BeginInvoke(action); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
        else
        {
            action();
        }
    }
}
