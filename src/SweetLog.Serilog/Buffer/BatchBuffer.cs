using SweetLib.Collections.Unsafe.Queue;
using SweetLib.Collections.Unsafe.Array;
using SweetLib.Collections.Unsafe.List;
using SweetLog.Serilog.Common.Enums;
using SweetLib.Collections.Unsafe;
using SweetLog.Serilog.Encoders;
using SweetLog.Serilog.Batching;
using System.Buffers.Binary;

namespace SweetLog.Serilog.Buffer;

public unsafe sealed class BatchBuffer : IDisposable
{
    private readonly SinkOptions options;
    private readonly HeartbeatEncoder heartEncoder;

    public UnsafeQueue<UnsafeArray<byte>> Queue;

    public UnsafeArray<byte> Heartbeat;

    public UnsafeList<byte> Batch;
    public LogBatch batchHeaders;

    public BatchBuffer(SinkOptions options)
    {
        Queue = new UnsafeQueue<UnsafeArray<byte>>(options.QueueCapacity);

        heartEncoder = new HeartbeatEncoder();
        Heartbeat = new UnsafeArray<byte>(1 + 8); // 1 messageType, 8 timestamp

        Batch = new UnsafeList<byte>(options.BatchSize);
        batchHeaders = new();

        InitBatch();
    }

    public void ResizeBatch(int newCapacity)
    {
        Batch.Dispose();

        Batch = new UnsafeList<byte>((uint)newCapacity);

        InitBatch();
    }

    public void WriteBatch()
    {
        var buffer = new UnsafeArray<byte>(Batch.Length);

        HardWrite(&buffer);

        Queue.Enqueue(in buffer);
    }

    private void HardWrite(UnsafeArray<byte>* buffer)
    {
        Span<byte> batch = Batch.AsSpan();

        batchHeaders.QueueIndex = Queue.Write;
        batchHeaders.Timestamp = DateTime.UtcNow.Ticks;

        batch[0] = (byte)MessageType.LogBatch;

        BinaryPrimitives.WriteUInt32LittleEndian(
            batch[1..], batchHeaders.QueueIndex);

        BinaryPrimitives.WriteInt64LittleEndian(
            batch[5..], batchHeaders.Timestamp);

        BinaryPrimitives.WriteInt32LittleEndian(
            batch[13..], batchHeaders.LogsCount);

        Batch.CopyTo(buffer);

        InitBatch();
    }

    public Memory<byte> ReadBatch()
    {
        if (Queue.TryInQueue(out UnsafeArray<byte> value))
        {
            var memoryManager = new UnmanagedMemoryManager<byte>(value.Data, (int)value.Length);

            return memoryManager.Memory;
        }
        else if (Batch.Length > 16)
        {
            var buffer = new UnsafeArray<byte>(Batch.Length);

            HardWrite(&buffer);

            Queue.Enqueue(buffer);

            if (Queue.TryInQueue(out UnsafeArray<byte> bytes))
            {
                var memoryManager = new UnmanagedMemoryManager<byte>(bytes.Data, (int)bytes.Length);

                return memoryManager.Memory;
            }

            return null;
        }

        return null;
    }

    public Memory<byte> BeatHeart()
    {
        var prepared = heartEncoder.GetPreparedHeartbeat();

        int position = 0;

        heartEncoder.Encode(in prepared, Heartbeat.AsSpan(), ref position);

        unsafe
        {
            return new UnmanagedMemoryManager<byte>(
                Heartbeat.Data, (int)Heartbeat.Length).Memory;
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

        Queue.Read = index;
        Queue.Length -= count;
    }

    private void InitBatch()
    {
        Batch.Length = 1  // messageType 
                      + 4 // id
                      + 8 // timestamp
                      + 4 // LogsCount
        ;

        batchHeaders.LogsCount = 0;
    }

    public void Dispose()
    {
        Heartbeat.Dispose();
        Batch.Dispose();

        for (uint i = 0; i < Queue.Data->Length; i++)
        {
            uint index = (Queue.Read + i) % Queue.Capacity;
            Queue.Data[index].Dispose();
        }

        Queue.Dispose();
    }
}
