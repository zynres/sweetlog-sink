using SweetLog.Serilog.Common.Data;
using SweetLog.Serilog.Encoders;
using SweetLog.Serilog.Buffer;
using Serilog.Events;
using Serilog.Core;

namespace SweetLog.Serilog;

public class Sink : ILogEventSink
{
    private readonly LogEncoder encoder = new();

    private readonly SinkOptions options;
    private readonly BatchBuffer buffer;

    public Sink(BatchBuffer buffer, SinkOptions options)
    {
        this.options = options;
        this.buffer = buffer;
    }

    public void Emit(LogEvent logEvent)
    {
        PreparedLog preparedLog = encoder.GetPreparedLog(logEvent);

        if (preparedLog.Size + buffer.Batch.Length > buffer.Batch.Capacity)
        {
            buffer.WriteBatch();

            Console.WriteLine("Checking size emit");

            if (preparedLog.Size > buffer.Batch.Capacity)
            {
                Console.WriteLine("ResizeBatch emit");
                buffer.ResizeBatch(preparedLog.Size);
            }
        }

        Span<byte> logBuffer = buffer.Batch.AsWritableSpan();

        int position = 0;

        Console.WriteLine("Start encoding emit");

        Console.WriteLine(
            @$"
                Destination length: {logBuffer.Length},
                position: {position},
                prepared size: {preparedLog.Size}, 
                batch capacity: {buffer.Batch.Capacity},
                batch length: {buffer.Batch.Length},
                TraceId: {preparedLog.TraceId?.ToHexString() ?? "null"},
                SpanId: {preparedLog.SpanId?.ToHexString() ?? "null"}");

        try
        {
            encoder.Encode(in preparedLog, logBuffer, ref position);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception from encoding: {ex}");
            throw;
        }

        Console.WriteLine("End encoding emit");

        buffer.Batch.Length += (uint)position;

        buffer.batchHeaders.LogsCount++;

        Console.WriteLine($"log writed in batch logs count: {buffer.batchHeaders.LogsCount} and batch length: {buffer.Batch.Length}");
    }
}
