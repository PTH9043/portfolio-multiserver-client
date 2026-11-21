# ChatClient — C# .NET 10 WinForms 멀티플레이어 채팅 데모 클라이언트

> **기준일**: 2026-11-22 · **스택**: C# / .NET 10 (`net10.0-windows`, WinForms) · `Google.Protobuf` 3.33.6 · 비동기 `TcpClient` · `CVTRACE1` / `.cvlog` 런타임 추적

C++20 `MultiServer`([../MultiServer](../MultiServer))와 **동일한 `.proto` 스키마에서 생성된 C# 코드를 직접 링크**해 통신하는 독립 데모 클라이언트입니다. UI 계층(`Form1`)과 TCP/Protobuf 통신 계층(`ChatClientConnection`, `PacketFramer`, `PacketStreamDecoder`)을 완전히 분리해 설계했습니다.

---

## 1. 주요 구조 및 파일 구성

| 파일 | 책임 |
|---|---|
| `Protocol/ChatProtocolLimits.cs` | C++ 서버(`Packet.h`, `ChatProtocolLimits.h`)와 동일한 프레임 한도(4B 헤더 · 1,020B 최대 본문), 아이디(4~20자 영문·숫자·`_`)·비밀번호(6~64자)·닉네임(2~12자 완성형 한글·영문·숫자)·채팅 200자 규칙, 10초 하트비트 주기, `ELoginResult`·`ERegisterResult`·`EJoinRoomResult` 한국어 안내 매핑 |
| `Network/PacketFramer.cs` | `[uint16 type LE][uint16 size LE] + Protobuf Body` 인코더(`PacketFramer`) 및 TCP 분할·병합 스트림 조립 + 1,020B 초과 프레임 거부 디코더(`PacketStreamDecoder`) |
| `Network/ChatClientConnection.cs` | 비동기 `TcpClient` 접속·송신 직렬화(`SemaphoreSlim`)·수신 루프·패킷 디스패치(`SC_REGISTER_RESULT`/`SC_LOGIN_RESULT`/`SC_CHAT`/`SC_ROOM_USER_COUNT`/`SC_ROOM_USERS`/`SC_ROOM_LIST`/`SC_JOIN_ROOM_RESULT`/`SC_PONG`)·정상 종료 및 오류 전파 (UI 비의존) |
| `Form1.cs` / `Form1.Designer.cs` | 상단 `아이디`/`비밀번호`/`[접속 / 로그인]`/`[회원가입]` 모달 대화창(`--db=mysql` 계정 인증 및 비밀번호 공란 시 `--db=off` 닉네임 호환), 반응형 `TableLayoutPanel` 기반 채팅·다중 방(4채널) 선택/이동·참가자 20명 페이징 UI, 10초 `CS_PING`/`SC_PONG` 하트비트 및 상태바 RTT(ms) 표시, 한글 IME `Enter` 안정화(`BeginInvoke`), GDI `Font` 캐시/해제 |
| `Diagnostics/ClientRuntimeTrace.cs` | C++ `MultiServer`와 비트 단위 동일한 `CVTRACE1`(64B 헤더 + 256B 슬롯) 이진 `.cvtrace` 및 JSONL `.cvlog` 기록기 (`AsyncLocal` 스코프 전파, Win32 `GetProcessTimes` `runId` 동기화) |
| `Program.cs` | 클라이언트 프로세스 시작/종료 수명 주기 및 `ClientRuntimeTrace` 초기화·정상 종료(`orderlyEnd`) 보장 |

---

## 2. 프로토콜 단일 출처 공유

- `ChatClient.csproj`는 별도로 `.proto` 생성 파일을 복사하지 않고 `../MultiServer/Protocol/Generated/CSharp/*.cs`(`PacketType.cs`, `Login.cs`, `Chat.cs`, `Room.cs`)를 직접 컴파일 링크합니다.
- 격리 빌드 시에는 `-p:ProtocolCSharpDir=<경로>` MSBuild 속성으로 생성 코드 위치를 주입할 수 있습니다.

---

## 3. 빌드 및 실행

### 사전 조건
- .NET 10 SDK (`net10.0-windows`)
- 먼저 [../MultiServer](../MultiServer)에서 `Build_Protocol.bat`으로 C# 프로토콜 코드가 생성되어 있어야 합니다.

### 빌드 (오류 0 / 경고 0)
```bat
dotnet build ChatClient.csproj -c Release
```

### 실행
1. Docker MySQL 8.4 실행 (계정 영속 모드 사용 시):
   ```bat
   docker compose -f ..\MultiServer\db\docker-compose.yml up -d
   ```
2. C++ 서버 실행:
   ```bat
   ..\MultiServer\Server\Bin\Release\ServerApp.exe 4 7777
   ```
   > MySQL 없이 빠르게 테스트하려면 `--db=off` 옵션을 추가합니다.
3. 클라이언트 실행 (2개 이상 동시 실행 가능):
   ```bat
   bin\Release\net10.0-windows\ChatClient.exe
   ```
4. 상단 `[회원가입]`으로 계정(아이디·비밀번호·닉네임)을 만든 뒤 `아이디`/`비밀번호` 입력 → `[접속 / 로그인]` 클릭(비밀번호를 비우면 `--db=off` 닉네임 호환 모드로 접속) → 채팅 송수신, 우측 상단 채널 콤보박스(`#1 로비` / `#2 자유` / `#3 전략` / `#4 거래`) `[방 이동]`, 20명 단위 참가자 페이징(`◀ 이전` / `다음 ▶`), 상단 상태바 실시간 Ping RTT 확인.

---

## 4. 검증 결과 요약

- **Debug / Release 빌드**: 오류 0건, 경고 0건.
- **C++ 서버 ↔ C# 클라이언트 라이브 E2E**: 3바이트 TCP 분할·2패킷 병합 복원, 한글 닉네임 로그인, 중복/규칙 위반 거절, 양방향 한글·ASCII 채팅 무손실, 25명 동시 접속 20명 단위 페이징(offset 0/20), 정상 종료(`orderlyEnd=True`) 확인.
- **ChangeViewer 통합 관측**: C# 정적 클래스 구조(8개 타입, `Form1` partial 병합), 의존성 13건, 공유 프로토콜 4종/한도 18종, 실시간 `.cvtrace`·`.cvlog` 실행 추적 확인.
