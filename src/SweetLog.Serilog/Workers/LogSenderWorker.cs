using Microsoft.Extensions.Hosting;
using SweetLog.Serilog.Encoders;
using SweetLog.Serilog.Buffer;
using System.Net.WebSockets;

namespace SweetLog.Serilog.Workers;

public class LogSenderWorker : BackgroundService
{
    private readonly HeartbeatEncoder encoder;

    private readonly SinkOptions options;
    private readonly BatchBuffer buffer;

    public LogSenderWorker(BatchBuffer buffer, SinkOptions options)
    {
        encoder = new();

        this.options = options;
        this.buffer = buffer;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var client = new ClientWebSocket();

        await client.ConnectAsync(new Uri(options.Uri.AbsoluteUri + "/ws"), token);

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(options.FlushInterval, token);

            ReadOnlyMemory<byte> batch = buffer.Read();

            if (batch.Length == 0)
                continue;

            await client.SendAsync(
                batch, WebSocketMessageType.Binary, true, token);
        }

        await client.CloseAsync(
            WebSocketCloseStatus.NormalClosure, "Bye", token);
    }
}
