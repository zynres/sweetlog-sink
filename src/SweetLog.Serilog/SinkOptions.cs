namespace SweetLog.Serilog;

public sealed class SinkOptions 
{
    public required Uri Endpoint { get; init; }
    public required string ApiKey { get; init; }

    public long MaxDiskBufferSize { get; init; } = 250 * 1024 * 1024;
    public long MaxRamBufferSize { get; init; } = 150 * 1024 * 1024;
    
    public int QueueCapacity { get; init; } = 96;
    public int BatchSize { get; init; } = 1 * 1024 * 1024;

    public TimeSpan FlushInterval { get; init; } =
        TimeSpan.FromSeconds(1);

    public TimeSpan HeartbeatInterval { get; init; } =
        TimeSpan.FromSeconds(5);
}
