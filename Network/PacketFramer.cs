using System.Buffers.Binary;
using ChatClient.Protocol;
using Google.Protobuf;
using Multiplayer.Protocol;

namespace ChatClient.Network;

/// <summary>
/// C++ 서버의 FPacketHeader([uint16 type LE][uint16 size LE] + Protobuf body) 규약을
/// 인코딩·디코딩하는 순수 프레이밍 유틸리티입니다. TCP 분할·병합 수신을 안전하게 처리합니다.
/// </summary>
public static class PacketFramer
{
    public static byte[] EncodeFrame(ECSPacket packetType, IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        int bodyLength = message.CalculateSize();
        if (bodyLength > ChatProtocolLimits.PacketMaxBodySize)
        {
            throw new InvalidOperationException(
                $"패킷 본문 크기({bodyLength}B)가 최대 허용치({ChatProtocolLimits.PacketMaxBodySize}B)를 초과했습니다.");
        }

        byte[] frame = new byte[ChatProtocolLimits.PacketHeaderSize + bodyLength];
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), (ushort)packetType);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2, 2), (ushort)bodyLength);

        if (bodyLength > 0)
            message.WriteTo(frame.AsSpan(ChatProtocolLimits.PacketHeaderSize, bodyLength));

        return frame;
    }
}

public sealed class PacketStreamDecoder
{
    private byte[] _buffer = new byte[ChatProtocolLimits.PacketBufferSize * 4];
    private int _readOffset;
    private int _writeOffset;

    public int BufferedBytes => _writeOffset - _readOffset;

    public void Reset()
    {
        _readOffset = 0;
        _writeOffset = 0;
    }

    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        CompactIfNeeded(data.Length);

        if (_buffer.Length - _writeOffset < data.Length)
        {
            int needed = BufferedBytes + data.Length;
            int nextSize = _buffer.Length;
            while (nextSize < needed)
                nextSize *= 2;

            byte[] grown = new byte[nextSize];
            if (BufferedBytes > 0)
                Buffer.BlockCopy(_buffer, _readOffset, grown, 0, BufferedBytes);

            _writeOffset = BufferedBytes;
            _readOffset = 0;
            _buffer = grown;
        }

        data.CopyTo(_buffer.AsSpan(_writeOffset));
        _writeOffset += data.Length;
    }

    /// <summary>
    /// 완전한 패킷 1개를 꺼냅니다. 프로토콜 위반 시 error에 사유를 채우고 false를 반환합니다.
    /// </summary>
    public bool TryPopPacket(out ushort packetType, out byte[] body, out string? error)
    {
        packetType = 0;
        body = Array.Empty<byte>();
        error = null;

        int available = BufferedBytes;
        if (available < ChatProtocolLimits.PacketHeaderSize)
            return false;

        ReadOnlySpan<byte> header = _buffer.AsSpan(_readOffset, ChatProtocolLimits.PacketHeaderSize);
        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(0, 2));
        ushort bodySize = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(2, 2));

        if (bodySize > ChatProtocolLimits.PacketMaxBodySize)
        {
            error = $"수신 패킷 크기({bodySize}B)가 허용 상한({ChatProtocolLimits.PacketMaxBodySize}B)을 초과했습니다.";
            return false;
        }

        int frameSize = ChatProtocolLimits.PacketHeaderSize + bodySize;
        if (available < frameSize)
            return false;

        packetType = type;
        if (bodySize > 0)
        {
            body = new byte[bodySize];
            Buffer.BlockCopy(_buffer, _readOffset + ChatProtocolLimits.PacketHeaderSize, body, 0, bodySize);
        }

        _readOffset += frameSize;
        if (_readOffset == _writeOffset)
        {
            _readOffset = 0;
            _writeOffset = 0;
        }

        return true;
    }

    private void CompactIfNeeded(int incomingBytes)
    {
        if (_readOffset == 0)
            return;

        if (_buffer.Length - _writeOffset >= incomingBytes)
            return;

        int remaining = BufferedBytes;
        if (remaining > 0)
            Buffer.BlockCopy(_buffer, _readOffset, _buffer, 0, remaining);

        _readOffset = 0;
        _writeOffset = remaining;
    }
}
