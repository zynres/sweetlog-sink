using SweetLog.Serilog.Common.Data;

namespace SweetLog.Serilog.Encoders;

public sealed class HeartbeatEncoder 
{   
    public PreparedHeartbeat GetPreparedHeartbeat() 
    {
        return default;
    }

    public void Encode(in PreparedHeartbeat prepared, Span<byte> destination, ref int position)
    {

    }
}
