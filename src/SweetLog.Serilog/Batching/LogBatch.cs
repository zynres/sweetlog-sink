namespace SweetLog.Serilog.Batching;

public struct LogBatch
{
    public uint QueueIndex;

    public long Timestamp;
    public int LogsCount;

}
