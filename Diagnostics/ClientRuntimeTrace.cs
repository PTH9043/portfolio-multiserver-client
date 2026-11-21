using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ChatClient.Diagnostics;

public enum TraceEventKind : uint
{
    Create = 1,
    Destroy = 2,
    Call = 3,
    Post = 4,
    Callback = 5,
    End = 6,
}

/// <summary>
/// ChangeViewer의 CVTRACE1(64B 헤더 + 256B 레코드) 이진 실행 추적(.cvtrace)과
/// JSONL 로그(.cvlog)를 생성해 C# WinForms 클라이언트의 객체 수명·호출·네트워크 이벤트를 실시간 추적합니다.
/// </summary>
public static class ClientRuntimeTrace
{
    private const int HeaderBytes = 64;
    private const int RecordBytes = 256;

    private static readonly object Sync = new();
    private static readonly AsyncLocal<ulong> CurrentScope = new();
    private static readonly AsyncLocal<ulong> CurrentObject = new();

    private static FileStream? _traceStream;
    private static FileStream? _logStream;
    private static long _lastObjectId;
    private static ulong _lastSequence;
    private static long _lastLogSeq;
    private static bool _started;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(
        IntPtr hProcess,
        out ulong creationTime,
        out ulong exitTime,
        out ulong kernelTime,
        out ulong userTime);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    public static ulong NextObjectId() => (ulong)Interlocked.Increment(ref _lastObjectId);

    public static void Start(string? overrideTraceDirectory = null)
    {
        lock (Sync)
        {
            if (_started)
                return;

            try
            {
                string dir = ResolveTraceDirectory(overrideTraceDirectory);
                Directory.CreateDirectory(dir);

                uint pid = (uint)Environment.ProcessId;
                ulong runId = GetProcessCreationFileTime();

                string tracePath = Path.Combine(dir, $"{runId}-{pid}.cvtrace");
                string logPath = Path.Combine(dir, $"{runId}-{pid}.cvlog");

                _traceStream = new FileStream(
                    tracePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);

                WriteHeader(_traceStream, pid, runId);
                _traceStream.Flush();

                _logStream = new FileStream(
                    logPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);

                _lastSequence = 0;
                _lastLogSeq = 0;
                _started = true;
            }
            catch
            {
                CloseStreamsNoLock();
            }
        }
    }

    public static void Stop(string source = "Program.cs", [CallerLineNumber] int line = 0)
    {
        lock (Sync)
        {
            if (!_started)
                return;

            try
            {
                WriteRecordNoLock(TraceEventKind.End, 0, 0, 0, "Program", "Main", source, line);
                _traceStream?.Flush();
                _logStream?.Flush();
            }
            catch
            {
                // 종료 경로에서 관찰 오류는 클라이언트를 중단시키지 않습니다.
            }
            finally
            {
                CloseStreamsNoLock();
                _started = false;
            }
        }
    }

    public static ScopeToken EnterScope(
        string typeName,
        string methodName,
        ulong objectId,
        string source,
        TraceEventKind kind = TraceEventKind.Call,
        [CallerLineNumber] int line = 0)
    {
        ulong prevScope = CurrentScope.Value;
        ulong prevObj = CurrentObject.Value;
        ulong seq = Record(kind, typeName, methodName, objectId, source, prevScope, prevObj, line);
        if (seq != 0)
        {
            CurrentScope.Value = seq;
            CurrentObject.Value = objectId;
        }
        return new ScopeToken(prevScope, prevObj, seq != 0);
    }

    public static ulong RecordEvent(
        TraceEventKind kind,
        string typeName,
        string methodName,
        ulong objectId,
        string source,
        [CallerLineNumber] int line = 0)
    {
        return Record(kind, typeName, methodName, objectId, source, CurrentScope.Value, CurrentObject.Value, line);
    }

    public static void Log(string level, string message, ulong objectId = 0)
    {
        lock (Sync)
        {
            if (!_started || _logStream is null)
                return;

            try
            {
                long seq = Interlocked.Increment(ref _lastLogSeq);
                ulong traceSeq = CurrentScope.Value != 0 ? CurrentScope.Value : _lastSequence;
                ulong obj = objectId != 0 ? objectId : CurrentObject.Value;

                var payload = new
                {
                    seq,
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    level = level.ToUpperInvariant(),
                    thread = GetCurrentThreadId(),
                    traceSeq,
                    objectId = obj.ToString(),
                    msg = message,
                };

                byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload);
                _logStream.Write(json, 0, json.Length);
                _logStream.WriteByte((byte)'\n');
                _logStream.Flush();
            }
            catch
            {
                // 관찰 로깅 실패 시 정상 동작을 유지합니다.
            }
        }
    }

    private static ulong Record(
        TraceEventKind kind,
        string typeName,
        string methodName,
        ulong objectId,
        string source,
        ulong parentSeq,
        ulong callerObj,
        int line)
    {
        lock (Sync)
        {
            if (!_started || _traceStream is null)
                return 0;

            try
            {
                return WriteRecordNoLock(kind, objectId, callerObj, parentSeq, typeName, methodName, source, line);
            }
            catch
            {
                return 0;
            }
        }
    }

    private static ulong WriteRecordNoLock(
        TraceEventKind kind,
        ulong objectId,
        ulong callerObj,
        ulong parentSeq,
        string typeName,
        string methodName,
        string source,
        int line)
    {
        if (_traceStream is null)
            return 0;

        ulong seq = ++_lastSequence;
        ulong fileTime = (ulong)DateTime.UtcNow.ToFileTimeUtc();
        uint threadId = GetCurrentThreadId();

        Span<byte> buf = stackalloc byte[RecordBytes];
        buf.Clear();

        BinaryPrimitives.WriteUInt64LittleEndian(buf.Slice(0, 8), seq);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.Slice(8, 8), fileTime);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.Slice(16, 8), objectId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.Slice(24, 8), callerObj);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.Slice(32, 8), parentSeq);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.Slice(40, 4), threadId);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.Slice(44, 4), line > 0 ? (uint)line : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.Slice(48, 4), (uint)kind);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.Slice(52, 4), 0u);

        WriteFixedUtf8(buf.Slice(56, 64), typeName);
        WriteFixedUtf8(buf.Slice(120, 64), methodName);
        WriteFixedUtf8(buf.Slice(184, 72), source.Replace('\\', '/'));

        _traceStream.Write(buf);
        _traceStream.Flush();
        return seq;
    }

    private static void WriteHeader(FileStream stream, uint pid, ulong runId)
    {
        Span<byte> hdr = stackalloc byte[HeaderBytes];
        hdr.Clear();
        Encoding.ASCII.GetBytes("CVTRACE1", hdr.Slice(0, 8));
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(8, 4), 1u);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(12, 4), HeaderBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(16, 4), RecordBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(20, 4), 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(24, 4), pid);
        BinaryPrimitives.WriteUInt32LittleEndian(hdr.Slice(28, 4), 0u);
        BinaryPrimitives.WriteUInt64LittleEndian(hdr.Slice(32, 8), runId);
        stream.Write(hdr);
    }

    private static void WriteFixedUtf8(Span<byte> dest, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        int len = Math.Min(utf8.Length, dest.Length - 1);
        utf8.AsSpan(0, len).CopyTo(dest);
    }

    private static ulong GetProcessCreationFileTime()
    {
        if (GetProcessTimes(GetCurrentProcess(), out ulong creation, out _, out _, out _) && creation > 0)
            return creation;
        return (ulong)DateTime.UtcNow.ToFileTimeUtc();
    }

    private static string ResolveTraceDirectory(string? overrideDir)
    {
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ChatClient.csproj")))
                return Path.Combine(dir.FullName, "ChangeViewerTrace");
            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "ChangeViewerTrace");
    }

    private static void CloseStreamsNoLock()
    {
        try { _traceStream?.Dispose(); } catch { }
        try { _logStream?.Dispose(); } catch { }
        _traceStream = null;
        _logStream = null;
    }

    public readonly struct ScopeToken : IDisposable
    {
        private readonly ulong _prevScope;
        private readonly ulong _prevObj;
        private readonly bool _active;

        internal ScopeToken(ulong prevScope, ulong prevObj, bool active)
        {
            _prevScope = prevScope;
            _prevObj = prevObj;
            _active = active;
        }

        public void Dispose()
        {
            if (!_active)
                return;
            CurrentScope.Value = _prevScope;
            CurrentObject.Value = _prevObj;
        }
    }
}
