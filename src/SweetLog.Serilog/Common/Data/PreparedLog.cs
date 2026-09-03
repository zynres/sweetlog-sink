using System.Diagnostics;

namespace SweetLog.Serilog.Common.Data;

public struct PreparedLog
{
    public int Size;

    public ActivityTraceId? TraceId;
    public ActivitySpanId? SpanId;

    public long Timestamp;

    public byte Level;

    public int MessageByteCount;
    public string Message;

    public int ExceptionByteCount;
    public string? Exception;

    public int PropertiesCount;

    public PreparedProperty[] Properties;
}
