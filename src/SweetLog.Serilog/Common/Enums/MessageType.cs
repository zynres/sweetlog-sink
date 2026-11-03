namespace SweetLog.Serilog.Common.Enums;

public enum MessageType : byte
{
    AskConf = 0,
    LogBatch = 1,
    Heartbeat = 2,
}
