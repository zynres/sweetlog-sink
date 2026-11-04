using SweetLog.Serilog.Buffer;
using System.Net.WebSockets;

namespace SweetLog.Serilog.Services.Senders;

public sealed class LogSender
{
    public async Task SendingLoopAsync(ClientWebSocket client, TimeSpan interval, BatchBuffer buffer, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(token) && client.State == WebSocketState.Open)
            {
                ReadOnlyMemory<byte> batchMemory = buffer.ReadBatch();

                if (batchMemory.Length <= 17)
                    continue;

                await client.SendAsync(batchMemory, WebSocketMessageType.Binary, true, token);

                Console.WriteLine($"Bytes Sended: {batchMemory.Length}");
            }
        }
        catch (OperationCanceledException) { }
    }
}
