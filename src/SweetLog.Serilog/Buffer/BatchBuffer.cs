using SweetLib.Collections.Unsafe.Concurrent.Queue;
using SweetLib.Collections.Unsafe.Array;
using SweetLib.Collections.Unsafe.List;

namespace SweetLog.Serilog.Buffer;

public sealed class BatchBuffer
{
    public readonly UnsafeConcurrentQueue<UnsafeArray<byte>> queue;
    public readonly UnsafeList<byte> batch;

    public BatchBuffer()
    {
        queue = new UnsafeConcurrentQueue<UnsafeArray<byte>>(96);
        batch = new UnsafeList<byte>(1 * 1024 * 1024);
    }

    public void Write(Span<byte> values, int written)
    {

    }

    public Span<byte> Read()
    {
        return new Span<byte>();
    }
}
