using SweetLog.Serilog.Serialization;
using SweetLog.Serilog.Common.Data;
using SweetLog.Serilog.Buffer;
using Serilog.Events;
using Serilog.Core;

namespace SweetLog.Serilog;

public class Sink : ILogEventSink
{
    private readonly BatchBuffer buffer = new();
    private readonly LogEncoder encoder = new();

    public void Emit(LogEvent logEvent)
    {
        PreparedLog preparedLog = encoder.GetPreparedLog(logEvent);

        Span<byte> logBuffer = stackalloc byte[preparedLog.Size];

        int written = encoder.Encode(in preparedLog, logBuffer);

        buffer.Write(logBuffer, written);
    }
}
