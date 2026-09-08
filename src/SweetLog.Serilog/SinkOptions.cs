namespace SweetLog.Serilog;

public sealed class SinkOptions
{
    public Uri Uri { get; init; } = new Uri("ws://localhost:5000");
    public string ApiKey { get; init; } = string.Empty;

    public long MaxDiskBufferSize { get; init; } = 1 * 1024 * 1024 * 1024; // 1 Gib
    public long MaxRamBufferSize { get; init; } = 150 * 1024 * 1024; // 150 Mib

    public int QueueCapacity { get; init; } = 96;
    public int BatchSize { get; init; } = 1 * 1024 * 1024; // 1 Mib

    public TimeSpan FlushInterval { get; init; } =
        TimeSpan.FromSeconds(1);

    public TimeSpan HeartbeatInterval { get; init; } =
        TimeSpan.FromSeconds(5);
}
