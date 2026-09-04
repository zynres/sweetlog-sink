using SweetLib.Collections.Unsafe.Concurrent.Queue;
using SweetLib.Collections.Unsafe.Array;
using SweetLib.Collections.Unsafe.List;
using SweetLog.Serilog.Batching;
using System.Buffers.Binary;
using SweetLib.Collections.Unsafe;

namespace SweetLog.Serilog.Buffer;

public unsafe sealed class BatchBuffer : IDisposable
{
    public UnsafeConcurrentQueue<UnsafeArray<byte>> Queue;

    public UnsafeList<byte> Batch;
    public LogBatch batchHeaders;

    public BatchBuffer()
    {
        Queue = new UnsafeConcurrentQueue<UnsafeArray<byte>>(96);
        Batch = new UnsafeList<byte>(1 * 1024 * 1024);
        batchHeaders = new();

        InitBatch();
    }

    public void ResizeBatch(int newCapacity)
    {
        Batch.Dispose();

        Batch = new UnsafeList<byte>((uint)newCapacity);

        InitBatch();
    }

    public void Write()
    {
        var buffer = new UnsafeArray<byte>(Batch.Length);

        HardWrite(&buffer);

        Queue.Enqueue(in buffer);

    }

    private void HardWrite(UnsafeArray<byte>* buffer)
    {
        Span<byte> batch = Batch.AsSpan();

        batchHeaders.Id = Queue.Write;
        batchHeaders.Timestamp = DateTime.UtcNow.Ticks;

        BinaryPrimitives.WriteInt64LittleEndian(
            batch, batchHeaders.Id);

        BinaryPrimitives.WriteInt64LittleEndian(
            batch[4..], batchHeaders.Timestamp);

        BinaryPrimitives.WriteInt64LittleEndian(
            batch[12..], batchHeaders.LogsCount);

        Batch.CopyTo(buffer);

        InitBatch();
    }

    public Memory<byte> Read()
    {
        if (Queue.TryInQueue(out UnsafeArray<byte> value))
        {
            var memoryManager = new UnmanagedMemoryManager<byte>(value.Data, (int)value.Length);

            return memoryManager.Memory;
        }
        else
        {
            var buffer = new UnsafeArray<byte>(Batch.Length);

            HardWrite(&buffer);

            var memoryManager = new UnmanagedMemoryManager<byte>(buffer.Data, (int)buffer.Length);

            Queue.Enqueue(buffer);

            return memoryManager.Memory;
        }
    }

    public void DeleteSaved(uint index)
    {
        index++;

        uint count;

        if (index >= Queue.Read)
            count = index - Queue.Read;
        else
            count = Queue.Capacity - Queue.Read + index;

        for (uint i = Queue.Read; i < Queue.Length; i++)
        {
            Queue.Data[i].Dispose();

            if (i == Queue.Capacity)
                i = 0;
        }

        Queue.SetReadLength(index, count);
    }

    private void InitBatch()
    {
        Batch.Length += 4 // Id
                      + 8 // timestamp
                      + 4 // LogsCount
        ;

        batchHeaders.LogsCount = 0;
    }

    public void Dispose()
    {
        Batch.Dispose();

        for (uint i = 0; i < Queue.Data->Length; i++)
            Queue.Data[i].Dispose();

        Queue.Dispose();
    }
}
