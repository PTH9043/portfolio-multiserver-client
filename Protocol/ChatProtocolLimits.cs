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

    public const uint DefaultRoomId = 1;
    public const int DefaultRoomMaxUsers = 1000;
    public const int HeartbeatIntervalMs = 10000;
    public const int UsernameMinChars = 4;
    public const int UsernameMaxChars = 20;
    public const int PasswordMinChars = 6;
    public const int PasswordMaxChars = 64;
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

    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrEmpty(username) || username.Length < UsernameMinChars || username.Length > UsernameMaxChars)
            return false;

        foreach (char ch in username)
        {
            bool ok = (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_';
            if (!ok)
                return false;
        }
        return true;
    }

    public static bool IsValidPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < PasswordMinChars || password.Length > PasswordMaxChars)
            return false;

        foreach (char ch in password)
        {
            if (ch < 0x21 || ch > 0x7E)
                return false;
        }
        return true;
    }

    public static string DescribeLoginResult(ELoginResult result) => result switch
    {
        ELoginResult.LoginResultSuccess => "로그인 성공",
        ELoginResult.LoginResultInvalidNickname => "닉네임 규칙 위반 (2~12자 한글·영문·숫자만 허용)",
        ELoginResult.LoginResultDuplicateNickname => "이미 사용 중인 닉네임입니다.",
        ELoginResult.LoginResultAlreadyLoggedIn => "이미 로그인된 연결입니다.",
        ELoginResult.LoginResultRoomFull => "기본 방 정원(1,000명)이 가득 찼습니다.",
        ELoginResult.LoginResultInvalidCredentials => "아이디 또는 비밀번호가 일치하지 않습니다.",
        ELoginResult.LoginResultDbError => "데이터베이스 처리 중 오류가 발생했습니다.",
        ELoginResult.LoginResultAlreadyOnline => "이미 다른 곳에서 접속 중인 계정입니다.",
        _ => $"알 수 없는 로그인 응답 ({result})"
    };

    public static string DescribeRegisterResult(ERegisterResult result) => result switch
    {
        ERegisterResult.RegisterResultSuccess => "회원가입이 완료되었습니다.",
        ERegisterResult.RegisterResultInvalidUsername => "아이디는 4~20자의 영문·숫자·밑줄(_)만 사용할 수 있습니다.",
        ERegisterResult.RegisterResultInvalidPassword => "비밀번호는 공백 없는 6~64자여야 합니다.",
        ERegisterResult.RegisterResultInvalidNickname => "닉네임은 2~12자의 완성형 한글·영문·숫자만 사용할 수 있습니다.",
        ERegisterResult.RegisterResultDuplicateUsername => "이미 사용 중인 아이디입니다.",
        ERegisterResult.RegisterResultDuplicateNickname => "이미 사용 중인 닉네임입니다.",
        ERegisterResult.RegisterResultDbError => "서버 DB가 비활성 상태이거나 일시 오류가 발생했습니다.",
        _ => $"알 수 없는 회원가입 응답 ({result})"
    };

    public static string DescribeJoinRoomResult(EJoinRoomResult result) => result switch
    {
        EJoinRoomResult.JoinRoomSuccess => "방 이동 성공",
        EJoinRoomResult.JoinRoomNotFound => "존재하지 않는 방입니다.",
        EJoinRoomResult.JoinRoomFull => "대상 방의 정원이 가득 찼습니다.",
        EJoinRoomResult.JoinRoomAlreadyInRoom => "이미 해당 방에 참여 중입니다.",
        EJoinRoomResult.JoinRoomTransferInProgress => "이전 방 이동 처리가 아직 진행 중입니다.",
        _ => $"알 수 없는 방 이동 응답 ({result})"
    };
}
