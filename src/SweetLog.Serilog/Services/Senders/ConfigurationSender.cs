using System.Net.WebSockets;
using SweetLib.Collections.Unsafe;
using SweetLib.Collections.Unsafe.Array;
using SweetLog.Serilog.Services.Encoders;

namespace SweetLog.Serilog.Services.Senders;

public sealed class ConfigurationSender
{
    private readonly ConfigurationEncoder encoder;

    public ConfigurationSender(ConfigurationEncoder encoder)
    {
        this.encoder = encoder;
    }

    public async Task SendAsync(ClientWebSocket client, CancellationToken token)
    {
        try
        {
            var buffer = new UnsafeArray<byte>(encoder.CalculateSize(out int clientName, out int apiKey));

            encoder.Encode(buffer.AsSpan(), clientName, apiKey);

            ReadOnlyMemory<byte> memory;

            unsafe
            {
                memory = new UnmanagedMemoryManager<byte>(
                    buffer.Data, (int)buffer.Length).Memory;
            }

            await client.SendAsync(memory, WebSocketMessageType.Binary, true, token);

            Console.WriteLine("Configuration Sended");
        }
        catch (OperationCanceledException) { }
    }
}
