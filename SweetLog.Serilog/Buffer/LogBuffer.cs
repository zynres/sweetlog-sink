using SweetLib.Collections.Unsafe.Concurrent.Queue;
using SweetLog.Serilog.Batching;

namespace SweetLog.Serilog.Buffer;

public sealed class LogBuffer : IDisposable
{
    private readonly UnsafeConcurrentQueue<LogBatch> queue;

    public LogBuffer()
    {
        queue = new UnsafeConcurrentQueue<LogBatch>(1024);
    }

    public void Enqueue(in LogBatch value)
    {
        queue.Enqueue(in value);
    }

    public bool TryDequeue(out LogBatch value)
    {
        return queue.TryDequeue(out value);
    }

    public void Dispose()
    {
        queue.Dispose();
    }
}
