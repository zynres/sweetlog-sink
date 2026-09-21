using SweetLog.Serilog.Common.Enums;
using SweetLog.Serilog.Common.Data;
using System.Buffers.Binary;

namespace SweetLog.Serilog.Encoders;

public sealed class HeartbeatEncoder
{
    public PreparedHeartbeat GetPreparedHeartbeat()
    {
        return new PreparedHeartbeat()
        {
            MessageType = (byte)MessageType.Heartbeat,
            Timestamp = DateTime.UtcNow.Ticks
        };
    }

    public void Encode(in PreparedHeartbeat prepared, Span<byte> destination, ref int position)
    {
        destination[position] = prepared.MessageType;

        position++;

        BinaryPrimitives.WriteInt64LittleEndian(
            destination[position..], prepared.Timestamp);

        position += 8;
    }
}
