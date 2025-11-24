using System;
using System.Collections.Generic;
using System.Globalization;
using Gurux.DLMS.Enums;
using Opc.Ua;

namespace BlueGate.Core.Models;

public static class MappingProfileDefaults
{
    private static readonly IReadOnlyDictionary<string, BuiltInType> ObisTypeDefaults = new Dictionary<string, BuiltInType>
    {
        ["1.0.1.8.0.255"] = BuiltInType.Double
    };

    public static bool EnsureDefaults(MappingProfile profile)
    {
        if (!HasDataType(profile))
        {
            var defaultType = GetDefaultBuiltInType(profile);
            if (defaultType is not null)
            {
                profile.BuiltInType ??= defaultType;
            }
        }

        if (profile.InitialValue is null && profile.BuiltInType is BuiltInType builtInType)
        {
            profile.InitialValue = GetDefaultValue(builtInType);
        }

        return HasDataType(profile);
    }

    public static bool HasDataType(MappingProfile profile) =>
        profile.BuiltInType is not null || !string.IsNullOrWhiteSpace(profile.DataTypeNodeId);

    public static BuiltInType? GetDefaultBuiltInType(MappingProfile profile)
    {
        if (ObisTypeDefaults.TryGetValue(profile.ObisCode, out var obisType))
            return obisType;

        return profile.ObjectType switch
        {
            ObjectType.Register => BuiltInType.Double,
            ObjectType.Clock => BuiltInType.DateTime,
            _ => null
        };
    }

    public static object? GetDefaultValue(BuiltInType builtInType) => builtInType switch
    {
        BuiltInType.Boolean => false,
        BuiltInType.SByte => (sbyte)0,
        BuiltInType.Byte => (byte)0,
        BuiltInType.Int16 => (short)0,
        BuiltInType.UInt16 => (ushort)0,
        BuiltInType.Int32 => 0,
        BuiltInType.UInt32 => (uint)0,
        BuiltInType.Int64 => (long)0,
        BuiltInType.UInt64 => (ulong)0,
        BuiltInType.Float => 0f,
        BuiltInType.Double => 0d,
        BuiltInType.String => string.Empty,
        BuiltInType.DateTime => DateTime.MinValue,
        _ => null
    };

    public static object? CoerceInitialValue(MappingProfile profile)
    {
        if (profile.InitialValue is null)
            return null;

        if (profile.BuiltInType is not BuiltInType builtInType)
            return profile.InitialValue;

        var rawValue = UnwrapJsonValue(profile.InitialValue);
        var targetType = TypeInfo.GetSystemType(builtInType, ValueRanks.Scalar);
        if (targetType is null)
            return rawValue;

        try
        {
            return Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return rawValue;
        }
    }

    private static object UnwrapJsonValue(object value)
    {
        return value switch
        {
            System.Text.Json.JsonElement jsonElement => jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number when jsonElement.TryGetInt64(out var longValue) => longValue,
                System.Text.Json.JsonValueKind.Number when jsonElement.TryGetDouble(out var doubleValue) => doubleValue,
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                _ => jsonElement.ToString()
            },
            _ => value
        };
    }
}
