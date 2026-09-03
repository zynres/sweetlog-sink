using SweetLog.Serilog.Common.Enums;
using SweetLog.Serilog.Common.Data;
using System.Buffers.Binary;
using Serilog.Events;
using System.Text;

namespace SweetLog.Serilog.Serialization;

public class LogEncoder
{
    public PreparedLog GetPreparedLog(LogEvent logEvent)
    {
        string message = logEvent.MessageTemplate.Render(logEvent.Properties);
        string? exception = logEvent.Exception?.ToString();

        PreparedLog preparedLog = new()
        {
            TraceId = logEvent.TraceId,
            SpanId = logEvent.SpanId,
            Timestamp = logEvent.Timestamp.ToUnixTimeMilliseconds(),
            Level = (byte)logEvent.Level,
            MessageByteCount = Encoding.UTF8.GetByteCount(message),
            Message = message,
            ExceptionByteCount = string.IsNullOrEmpty(exception) == true ? 0 : Encoding.UTF8.GetByteCount(exception),
            Exception = exception,
            Properties = new PreparedProperty[logEvent.Properties.Count]
        };

        int index = 0;

        int propertiesByteCount = 0;

        foreach (var property in logEvent.Properties)
        {
            if (property.Value is not ScalarValue value)
                continue;

            PropertyType type = PropertyType.Null;

            int nameByteCount = Encoding.UTF8.GetByteCount(property.Key);
            int valueByteCount = GetScalarSize(value, out type);

            PreparedProperty preparedProperty = new()
            {
                NameByteCount = nameByteCount,
                Name = property.Key,
                ValueByteCount = valueByteCount,
                Value = property.Value,
                Type = type
            };

            preparedLog.Properties[index] = preparedProperty;

            propertiesByteCount += 1 // type
                + 4                  // nameByteCount
                + nameByteCount      // name
                + 4                  // valueByteCount
                + valueByteCount     // value
            ;

            index++;
        }

        preparedLog.PropertiesCount = index;

        preparedLog.Size = 8 // timestamp
            + 16             // traceId
            + 8              // spanId
            + 1              // level
            + 4              // messageByteCount 
            + preparedLog.MessageByteCount
            + 4              // exceptionByteCount
            + preparedLog.ExceptionByteCount
            + 4              // propertiesCount
            + propertiesByteCount
        ;

        return preparedLog;
    }

    public int Encode(in PreparedLog preparedLog, Span<byte> destination)
    {
        int position = 0;

        BinaryPrimitives.WriteInt64LittleEndian(
            destination[position..], preparedLog.Timestamp);

        position += 8;

        if (preparedLog.TraceId == null)
            destination.Slice(position, 16).Clear();
        else
            preparedLog.TraceId.Value.CopyTo(destination[position..]);

        position += 16;

        if (preparedLog.SpanId == null)
            destination.Slice(position, 8).Clear();
        else
            preparedLog.SpanId.Value.CopyTo(destination[position..]);

        position += 8;

        destination[position] = preparedLog.Level;

        position++;

        BinaryPrimitives.WriteInt32LittleEndian(
            destination[position..], preparedLog.MessageByteCount);

        position += 4;

        if (preparedLog.MessageByteCount > 0)
        {
            Encoding.UTF8.GetBytes(preparedLog.Message, destination[position..]);

            position += preparedLog.MessageByteCount;
        }

        BinaryPrimitives.WriteInt32LittleEndian(
            destination[position..], preparedLog.ExceptionByteCount);

        position += 4;

        if (preparedLog.ExceptionByteCount > 0)
        {
            Encoding.UTF8.GetBytes(preparedLog.Exception, destination[position..]);

            position += preparedLog.ExceptionByteCount;
        }

        BinaryPrimitives.WriteInt32LittleEndian(
            destination[position..], preparedLog.PropertiesCount);

        position += 4;

        for (int i = 0; i < preparedLog.PropertiesCount; i++)
        {
            ref readonly PreparedProperty property = ref preparedLog.Properties[i];

            BinaryPrimitives.WriteInt32LittleEndian(
                destination[position..], property.NameByteCount);

            position += 4;

            if (property.NameByteCount > 0)
            {
                Encoding.UTF8.GetBytes(property.Name, destination[position..]);

                position += property.NameByteCount;
            }

            destination[position] = (byte)property.Type;

            position++;

            BinaryPrimitives.WriteInt32LittleEndian(
                destination[position..], property.ValueByteCount);

            position += 4;

            WritePropertyValue(
                property, destination[position..]);

            position += property.ValueByteCount;
        }

        return position;
    }

    private int GetScalarSize(ScalarValue scalar, out PropertyType type)
    {
        object? value = scalar.Value;

        switch (value)
        {
            case null:
                type = PropertyType.Null;
                return 0;

            case string str:
                type = PropertyType.String;
                return Encoding.UTF8.GetByteCount(str);

            case int:
                type = PropertyType.Int32;
                return 4;

            case uint:
                type = PropertyType.UInt32;
                return 4;

            case long:
                type = PropertyType.Int64;
                return 8;

            case ulong:
                type = PropertyType.UInt64;
                return 8;

            case bool:
                type = PropertyType.Boolean;
                return 1;

            case double:
                type = PropertyType.Double;
                return 8;

            case float:
                type = PropertyType.Float;
                return 4;

            case Guid:
                type = PropertyType.Guid;
                return 16;

            case DateTime:
                type = PropertyType.DateTime;
                return 8;

            default:
                throw new NotSupportedException(
                    $"Property type {value.GetType()} is not supported.");
        }
    }

    private void WritePropertyValue(PreparedProperty property, Span<byte> destination)
    {
        ScalarValue scalar = (ScalarValue)property.Value;

        switch (property.Type)
        {
            case PropertyType.Int32:
                BinaryPrimitives.WriteInt32LittleEndian(
                    destination,
                    (int)scalar.Value!);
                break;

            case PropertyType.Int64:
                BinaryPrimitives.WriteInt64LittleEndian(
                    destination,
                    (long)scalar.Value!);
                break;

            case PropertyType.UInt32:
                BinaryPrimitives.WriteUInt32LittleEndian(
                    destination,
                    (uint)scalar.Value!);
                break;

            case PropertyType.UInt64:
                BinaryPrimitives.WriteUInt64LittleEndian(
                    destination,
                    (ulong)scalar.Value!);
                break;

            case PropertyType.Boolean:
                destination[0] = (bool)scalar.Value! ? (byte)1 : (byte)0;
                break;

            case PropertyType.Guid:
                ((Guid)scalar.Value!).TryWriteBytes(destination);
                break;

            case PropertyType.String:
                if (property.ValueByteCount > 0)
                    Encoding.UTF8.GetBytes((string)scalar.Value!, destination);
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported property type: {property.Type}");
        }
    }

}
