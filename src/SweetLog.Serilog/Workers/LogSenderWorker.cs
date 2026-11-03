using SweetLib.Collections.Unsafe.Array;
using SweetLib.Collections.Unsafe.List;
using SweetLog.Serilog.Common.Enums;
using Microsoft.Extensions.Hosting;
using SweetLib.Collections.Unsafe;
using SweetLog.Serilog.Buffer;
using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;

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
        receivingBuffer = new UnsafeList<byte>(5); // Queue Index

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

                Console.WriteLine($"Bytes Sended: {batchMemory.Length}");
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

                Console.WriteLine("Heartbeat Sended");
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task ConfigurationSendingAsync(ClientWebSocket client, CancellationToken token)
    {
        try
        {
            var configurationBuffer = new UnsafeArray<byte>(CalculateConfigurationSize(out int clientName, out int apiKey));

            ConfigurationEncode(configurationBuffer.AsSpan(), clientName, apiKey);

            ReadOnlyMemory<byte> configurationMemory;

            unsafe
            {
                configurationMemory = new UnmanagedMemoryManager<byte>(
                    configurationBuffer.Data, (int)configurationBuffer.Length).Memory;
            }

            await client.SendAsync(configurationMemory, WebSocketMessageType.Binary, true, token);

            Console.WriteLine("Configuration Sended");
        }
        catch (OperationCanceledException) { }
    }

    private uint CalculateConfigurationSize(out int clientName, out int apiKey)
    {
        clientName = string.IsNullOrEmpty(options.ClientName) == true ? 0 : Encoding.UTF8.GetByteCount(options.ClientName);
        apiKey = string.IsNullOrEmpty(options.ApiKey) == true ? 0 : Encoding.UTF8.GetByteCount(options.ApiKey);

        int size = 1 + // MessageType
                   4 + // clientNameByteCount
                   clientName +
                   4 + // apiKeyByteCount
                   apiKey +
                   4;          // batchSize

        return (uint)size;
    }

    private void ConfigurationEncode(Span<byte> destination, int clientNameByteCount, int apiKeyByteCount)
    {
        int position = 0;

        destination[position] = (byte)MessageType.AskConf;

        position++;

        BinaryPrimitives.WriteInt32LittleEndian(
            destination[position..], clientNameByteCount);

        position += 4;

        if (clientNameByteCount > 0)
        {
            Encoding.UTF8.GetBytes(options.ClientName, destination[position..]);

            position += clientNameByteCount;
        }

        BinaryPrimitives.WriteInt32LittleEndian(
            destination[position..], apiKeyByteCount);

        position += 4;

        if (apiKeyByteCount > 0)
        {
            Encoding.UTF8.GetBytes(options.ApiKey, destination[position..]);

            position += apiKeyByteCount;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(
            destination[position..], options.BatchSize);

        // position += 4;
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
                    _ = ConfigurationSendingAsync(client, token);
                }

                receivingBuffer.Length = 0;
            }
        }
    }
}
