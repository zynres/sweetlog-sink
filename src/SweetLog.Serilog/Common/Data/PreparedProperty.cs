using SweetLog.Serilog.Common.Enums;

namespace SweetLog.Serilog.Common.Data;

public struct PreparedProperty
{
    public string Name;
    public int NameByteCount;

    public PropertyType Type;

    public int ValueByteCount;
    public object Value;
}
