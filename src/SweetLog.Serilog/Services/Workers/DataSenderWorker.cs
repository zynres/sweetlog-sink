using SweetLog.Serilog.Services.Senders;
using SweetLib.Collections.Unsafe.List;
using SweetLog.Serilog.Common.Enums;
using Microsoft.Extensions.Hosting;
using SweetLib.Collections.Unsafe;
using SweetLog.Serilog.Buffer;
using System.Buffers.Binary;
using System.Net.WebSockets;

namespace SweetLog.Serilog.Services.Workers;

public class DataSenderWorker : BackgroundService
{
    private readonly SinkOptions options;
    private readonly BatchBuffer buffer;

    private readonly ConfigurationSender configurationSender;
    private readonly HeartbeatSender heartbeatSender;
    private readonly LogSender logSender;

    private UnsafeList<byte> receivingBuffer;
    private readonly Memory<byte> receivingMemory;

    public DataSenderWorker(BatchBuffer buffer, SinkOptions options, ConfigurationSender configurationSender, HeartbeatSender heartbeatSender, LogSender logSender)
    {
        this.options = options;
        this.buffer = buffer;

        this.configurationSender = configurationSender;
        this.heartbeatSender = heartbeatSender;
        this.logSender = logSender;

        receivingBuffer = new UnsafeList<byte>(22);

        unsafe
        {
            receivingMemory = new UnmanagedMemoryManager<byte>(
                receivingBuffer.Data, (int)receivingBuffer.Capacity).Memory;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
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
        Task flushLoop = logSender.SendingLoopAsync(client, options.FlushInterval, buffer, token);
        Task heartbeatLoop = heartbeatSender.SendingLoopAsync(client, options.HeartbeatInterval, token);

        await Task.WhenAll(flushLoop, heartbeatLoop);
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
                if (receivingBuffer[0] == (byte)MessageType.Heartbeat)
                {
                    uint queueIndex = BinaryPrimitives.ReadUInt32LittleEndian(
                            receivingBuffer.AsSpan()[1..]);

                    Console.WriteLine($"Receive heartbeat. Delete batches to index: {queueIndex}");

                    buffer.DeleteSaved(queueIndex);
                }
                else
                {
                    _ = configurationSender.SendAsync(client, token);
                }

                receivingBuffer.Length = 0;
            }
        }
    }
}
