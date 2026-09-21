namespace SweetLog.Serilog;

public sealed class SinkOptions
{
    public Uri Uri { get; set; } = new Uri("ws://log:8000");
    public string ApiKey { get; set; } = string.Empty;

    public long MaxDiskBufferSize { get; set; } = 1 * 1024 * 1024 * 1024; // 1 Gib
    public long MaxRamBufferSize { get; set; } = 150 * 1024 * 1024; // 150 Mib

    public uint QueueCapacity { get; set; } = 96;
    public uint BatchSize { get; set; } = 1 * 1024 * 1024; // 1 Mib

    public TimeSpan FlushInterval { get; set; } =
        TimeSpan.FromSeconds(1);

    public TimeSpan HeartbeatInterval { get; set; } =
        TimeSpan.FromSeconds(5);
}
