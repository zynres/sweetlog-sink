using SweetLog.Serilog.Common.Enums;
using System.Buffers.Binary;
using System.Text;

namespace SweetLog.Serilog.Services.Encoders;

public sealed class ConfigurationEncoder
{
    private readonly SinkOptions options;

    public ConfigurationEncoder(SinkOptions options)
    {
        this.options = options;
    }

    public uint CalculateSize(out int clientName, out int apiKey)
    {
        clientName = string.IsNullOrEmpty(options.ClientName) == true ? 0 : Encoding.UTF8.GetByteCount(options.ClientName);
        apiKey = string.IsNullOrEmpty(options.ApiKey) == true ? 0 : Encoding.UTF8.GetByteCount(options.ApiKey);

        int size = 1 + // MessageType
                   4 + // clientNameByteCount
                   clientName +
                   4 + // apiKeyByteCount
                   apiKey +
                   4;  // batchSize

        return (uint)size;
    }

    public void Encode(Span<byte> destination, int clientNameByteCount, int apiKeyByteCount)
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
}
