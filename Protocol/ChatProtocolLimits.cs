using System.Text;
using Multiplayer.Protocol;

namespace ChatClient.Protocol;

/// <summary>
/// C++ 서버(Core/Public/Packet.h, Server/Public/ChatProtocolLimits.h)와 동일한
/// 패킷 크기 상한 및 입력 검증 규칙을 정의합니다.
/// </summary>
public static class ChatProtocolLimits
{
    public const int PacketHeaderSize = 4;
    public const int PacketBufferSize = 1024;
    public const int PacketMaxBodySize = PacketBufferSize - PacketHeaderSize;

    public const int DefaultRoomMaxUsers = 1000;
    public const int NicknameMinChars = 2;
    public const int NicknameMaxChars = 12;
    public const int ChatMinChars = 1;
    public const int ChatMaxChars = 200;
    public const int RoomUsersPageSize = 20;

    /// <summary>
    /// 닉네임 규칙: 2~12자의 완성형 한글(U+AC00..U+D7A3)·영문·숫자만 허용합니다.
    /// </summary>
    public static bool IsValidNickname(string? nickname)
    {
        if (string.IsNullOrEmpty(nickname))
            return false;

        int runeCount = 0;
        foreach (Rune rune in nickname.EnumerateRunes())
        {
            int cp = rune.Value;
            bool allowed =
                (cp >= 0xAC00 && cp <= 0xD7A3) ||
                (cp >= 'A' && cp <= 'Z') ||
                (cp >= 'a' && cp <= 'z') ||
                (cp >= '0' && cp <= '9');

            if (!allowed)
                return false;

            runeCount++;
            if (runeCount > NicknameMaxChars)
                return false;
        }

        return runeCount >= NicknameMinChars && runeCount <= NicknameMaxChars;
    }

    /// <summary>
    /// 채팅 본문 규칙: 유효한 유니코드 스칼라 1~200자이며 UTF-8 직렬화 시 패킷 한도 이내여야 합니다.
    /// </summary>
    public static bool IsValidChatText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        int runeCount = CountRunes(text);
        return runeCount >= ChatMinChars && runeCount <= ChatMaxChars;
    }

    public static int CountRunes(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int count = 0;
        foreach (Rune _ in text.EnumerateRunes())
            count++;
        return count;
    }

    public static string DescribeLoginResult(ELoginResult result) => result switch
    {
        ELoginResult.LoginResultSuccess => "로그인 성공",
        ELoginResult.LoginResultInvalidNickname => "닉네임 규칙 위반 (2~12자 한글·영문·숫자만 허용)",
        ELoginResult.LoginResultDuplicateNickname => "이미 사용 중인 닉네임입니다.",
        ELoginResult.LoginResultAlreadyLoggedIn => "이미 로그인된 연결입니다.",
        ELoginResult.LoginResultRoomFull => "기본 방 정원(1,000명)이 가득 찼습니다.",
        _ => $"알 수 없는 로그인 응답 ({result})"
    };
}
