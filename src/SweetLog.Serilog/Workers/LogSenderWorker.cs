using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;
using SweetLog.Serilog.Buffer;

namespace SweetLog.Serilog.Workers;

public class LogSenderWorker : BackgroundService
{
    private readonly SinkOptions options;
    private readonly BatchBuffer buffer;

    public LogSenderWorker(BatchBuffer buffer, SinkOptions options)
    {
        this.options = options;
        this.buffer = buffer;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var client = new ClientWebSocket();

        await client.ConnectAsync(new Uri(options.Uri.AbsoluteUri + "/ws"), token);

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(options.FlushInterval);

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
