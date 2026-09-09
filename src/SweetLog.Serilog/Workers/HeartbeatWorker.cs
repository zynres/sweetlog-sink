using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;

namespace SweetLog.Serilog.Workers;

public class HeartbeatWorker : BackgroundService
{
    private readonly SinkOptions options;

    public HeartbeatWorker(SinkOptions options)
    {
        this.options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var client = new ClientWebSocket();

        await client.ConnectAsync(new Uri(options.Uri.AbsoluteUri + "/status"), token);

        while (!token.IsCancellationRequested)
        {
            //await client.SendAsync();

            await Task.Delay(options.HeartbeatInterval);
        }

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", token);
    }
}
