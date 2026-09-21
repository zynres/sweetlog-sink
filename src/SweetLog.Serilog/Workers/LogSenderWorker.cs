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
        var flushTask = Task.Delay(options.FlushInterval, token);
        var heartbeatTask = Task.Delay(options.HeartbeatInterval, token);

        receivingBuffer = new UnsafeList<byte>(4); // Queue Index

        unsafe
        {
            receivingMemory = new UnmanagedMemoryManager<byte>(
                receivingBuffer.Data, (int)receivingBuffer.Capacity).Memory;
        }

        while (!token.IsCancellationRequested)
        {
            using var client = new ClientWebSocket();

            try
            {
                await client.ConnectAsync(
                    new Uri(options.Uri.AbsoluteUri + "ws"),
                    token);

                Console.WriteLine("Client connected");

                while (!token.IsCancellationRequested && client.State == WebSocketState.Open)
                {
                    var completed = await Task.WhenAny(flushTask, heartbeatTask);

                    if (completed == flushTask)
                    {
                        ReadOnlyMemory<byte> batchMemory = buffer.ReadBatch();

                        if (batchMemory.Length == 0)
                            continue;

                        await client.SendAsync(
                            batchMemory, WebSocketMessageType.Binary, true, token);

                        Console.WriteLine($"bytes sended: {batchMemory.Length}");

                        flushTask = Task.Delay(options.FlushInterval, token);
                    }
                    else if (completed == heartbeatTask)
                    {
                        ReadOnlyMemory<byte> heartbeatMemory = buffer.BeatHeart();

                        await client.SendAsync(
                            heartbeatMemory, WebSocketMessageType.Binary, true, token);

                        await ReceiveHearbeatResponse(client, token);

                        buffer.DeleteSaved(
                            BinaryPrimitives.ReadUInt32LittleEndian(
                                receivingBuffer.AsSpan()));

                        heartbeatTask = Task.Delay(options.HeartbeatInterval, token);
                    }
                }
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
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Application closing", CancellationToken.None);
            }
        }
    }

    private async Task ReceiveHearbeatResponse(ClientWebSocket client, CancellationToken token)
    {
        bool endOfReceivedMessage = false;

        while (endOfReceivedMessage)
        {
            var result = await client.ReceiveAsync(
                receivingMemory[(int)receivingBuffer.Length..], token);

            receivingBuffer.Length += (uint)result.Count;

            endOfReceivedMessage = result.EndOfMessage;
        }
    }
}
