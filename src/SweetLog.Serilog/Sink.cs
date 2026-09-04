using SweetLog.Serilog.Serialization;
using SweetLog.Serilog.Common.Data;
using SweetLog.Serilog.Buffer;
using Serilog.Events;
using Serilog.Core;

namespace SweetLog.Serilog;

public class Sink : ILogEventSink
{
    private readonly BatchBuffer buffer;
    private readonly LogEncoder encoder = new();

    public Sink(BatchBuffer buffer)
    {
        this.buffer = buffer;
    }

    public void Emit(LogEvent logEvent)
    {
        PreparedLog preparedLog = encoder.GetPreparedLog(logEvent);

        if (preparedLog.Size + buffer.Batch.Length > buffer.Batch.Capacity)
        {
            buffer.Write();

            if (preparedLog.Size > buffer.Batch.Capacity)
            {
                buffer.ResizeBatch(preparedLog.Size);
            }
        }

        Span<byte> logBuffer = buffer.Batch.AsWritableSpan();

        int position = (int)buffer.Batch.Length;

        encoder.Encode(in preparedLog, logBuffer, ref position);

        buffer.Batch.Length += (uint)(position - buffer.Batch.Length);

        buffer.batchHeaders.LogsCount++;
    }
}
