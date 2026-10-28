using SweetLib.Collections.Unsafe.List;
using Microsoft.Extensions.Hosting;
using SweetLib.Collections.Unsafe;
using SweetLog.Serilog.Buffer;
using System.Buffers.Binary;
using System.Net.WebSockets;

namespace SweetLog.Serilog.Workers;

public class LogSenderWorker : BackgroundService
{
    private readonly SinkOptions options;
    private readonly BatchBuffer buffer;

    private UnsafeList<byte> receivingBuffer;
    private Memory<byte> receivingMemory;

    public LogSenderWorker(BatchBuffer buffer, SinkOptions options)
    {
        this.options = options;
        this.buffer = buffer;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        receivingBuffer = new UnsafeList<byte>(4); // Queue Index

        unsafe
        {
            receivingMemory = new UnmanagedMemoryManager<byte>(
                receivingBuffer.Data, (int)receivingBuffer.Capacity).Memory;
        }

        bool connected = false;

        while (!token.IsCancellationRequested)
        {
            using var client = new ClientWebSocket();

            try
            {
                await client.ConnectAsync(
                    new Uri(options.Uri.AbsoluteUri + "ws"),
                    token);

                connected = true;

                Console.WriteLine("Client connected");

                _ = SendingLoop(client, token);
                await ReceiveLoop(client, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Console.WriteLine("Log sender cancelled");

                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                await Task.Delay(1000, token);
            }
            finally
            {
                if (connected && client.State == WebSocketState.Open ||
                    client.State == WebSocketState.CloseReceived)
                {
                    await client.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Application closing",
                        CancellationToken.None);

                    connected = false;
                }
            }
        }
    }

    private async Task SendingLoop(ClientWebSocket client, CancellationToken token)
    {
        Task flushLoop = FlushLoopAsync(client, token);
        Task heartbeatLoop = HeartbeatLoopAsync(client, token);

        await Task.WhenAll(flushLoop, heartbeatLoop);
    }

    private async Task FlushLoopAsync(ClientWebSocket client, CancellationToken token)
    {
        using var timer = new PeriodicTimer(options.FlushInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(token) && client.State == WebSocketState.Open)
            {
                ReadOnlyMemory<byte> batchMemory = buffer.ReadBatch();

                if (batchMemory.Length <= 17)
                    continue;

                await client.SendAsync(batchMemory, WebSocketMessageType.Binary, true, token);

                Console.WriteLine($"bytes sended: {batchMemory.Length}");
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket client, CancellationToken token)
    {
        using var timer = new PeriodicTimer(options.HeartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(token) && client.State == WebSocketState.Open)
            {
                ReadOnlyMemory<byte> heartbeatMemory = buffer.BeatHeart();

                await client.SendAsync(heartbeatMemory, WebSocketMessageType.Binary, true, token);

                Console.WriteLine("heartbeat sended");
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task ReceiveLoop(ClientWebSocket client, CancellationToken token)
    {
        while (!token.IsCancellationRequested && client.State == WebSocketState.Open)
        {
            var result = await client.ReceiveAsync(
                receivingMemory[(int)receivingBuffer.Length..], token);

            receivingBuffer.Length += (uint)result.Count;

            if (result.EndOfMessage)
            {
                uint queueIndex = BinaryPrimitives.ReadUInt32LittleEndian(
                        receivingBuffer.AsSpan());

                Console.WriteLine($"receive heartbeat delete batches to index: {queueIndex}");

                buffer.DeleteSaved(queueIndex);

                receivingBuffer.Length = 0;
            }
        }
    }
}
