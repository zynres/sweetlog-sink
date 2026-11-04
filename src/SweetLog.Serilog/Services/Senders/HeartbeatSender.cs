using System.Net.WebSockets;
using SweetLib.Collections.Unsafe;
using SweetLib.Collections.Unsafe.Array;
using SweetLog.Serilog.Buffer;
using SweetLog.Serilog.Services.Encoders;

namespace SweetLog.Serilog.Services.Senders;

public sealed class HeartbeatSender : IDisposable
{
    private readonly HeartbeatEncoder encoder;
    private UnsafeArray<byte> buffer;

    public HeartbeatSender(HeartbeatEncoder encoder) 
    {
        this.encoder = encoder;

        buffer = new UnsafeArray<byte>(1 + 8); // 1 messageType, 8 timestamp
    }

    public async Task SendingLoopAsync(ClientWebSocket client, TimeSpan interval, CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(token) && client.State == WebSocketState.Open)
            {
                ReadOnlyMemory<byte> heartbeatMemory = BeatHeart();

                await client.SendAsync(heartbeatMemory, WebSocketMessageType.Binary, true, token);

                Console.WriteLine("Heartbeat Sended");
            }
        }
        catch (OperationCanceledException) { }
    }

    private Memory<byte> BeatHeart()
    {
        var prepared = encoder.GetPreparedHeartbeat();

        int position = 0;

        encoder.Encode(in prepared, buffer.AsSpan(), ref position);

        unsafe
        {
            return new UnmanagedMemoryManager<byte>(
                buffer.Data, (int)buffer.Length).Memory;
        }
    }

    public void Dispose() 
    {
        buffer.Dispose();
    }

}
