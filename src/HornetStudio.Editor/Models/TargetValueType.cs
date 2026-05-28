using System;

namespace HornetStudio.Editor.Models;

/// <summary>
/// Defines the canonical registry target value types used by metadata.
/// </summary>
public enum TargetValueType
{
    Unknown,
    String,
    Bool,
    Int,
    Long,
    Float,
    Double,
    Decimal,
    Epoch,
    Bits,
    Object
}

/// <summary>
/// Parses and normalizes registry target value type metadata.
/// </summary>
public static class TargetValueTypes
{
    /// <summary>
    /// Parses one registry target type text into the canonical value.
    /// </summary>
    /// <param name="value">The raw type text.</param>
    /// <returns>The parsed canonical target value type.</returns>
    public static TargetValueType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TargetValueType.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "unknown" => TargetValueType.Unknown,
            "string" => TargetValueType.String,
            "bool" or "boolean" => TargetValueType.Bool,
            "int" or "integer" => TargetValueType.Int,
            "long" => TargetValueType.Long,
            "float" or "single" => TargetValueType.Float,
            "double" => TargetValueType.Double,
            "decimal" => TargetValueType.Decimal,
            "epoch" or "timestamp" => TargetValueType.Epoch,
            "bits" or "bitfield" => TargetValueType.Bits,
            "object" => TargetValueType.Object,
            _ => TargetValueType.Unknown
        };
    }

    /// <summary>
    /// Infers one registry target type from CLR type metadata or a runtime sample value.
    /// </summary>
    /// <param name="targetType">The runtime type, if known.</param>
    /// <param name="sampleValue">The runtime sample value, if known.</param>
    /// <returns>The inferred canonical target value type.</returns>
    public static TargetValueType Infer(Type? targetType, object? sampleValue)
    {
        var effectiveType = Nullable.GetUnderlyingType(targetType ?? sampleValue?.GetType() ?? typeof(object))
                            ?? targetType
                            ?? sampleValue?.GetType();
        if (effectiveType is not null)
        {
            if (effectiveType == typeof(string))
            {
                return TargetValueType.String;
            }

            if (effectiveType == typeof(bool))
            {
                return TargetValueType.Bool;
            }

            if (effectiveType == typeof(float))
            {
                return TargetValueType.Float;
            }

            if (effectiveType == typeof(double))
            {
                return TargetValueType.Double;
            }

            if (effectiveType == typeof(decimal))
            {
                return TargetValueType.Decimal;
            }

            if (effectiveType == typeof(long) || effectiveType == typeof(ulong))
            {
                return TargetValueType.Long;
            }

            if (effectiveType == typeof(byte)
                || effectiveType == typeof(sbyte)
                || effectiveType == typeof(short)
                || effectiveType == typeof(ushort)
                || effectiveType == typeof(int)
                || effectiveType == typeof(uint))
            {
                return TargetValueType.Int;
            }
        }

        return sampleValue switch
        {
            string => TargetValueType.String,
            bool => TargetValueType.Bool,
            float => TargetValueType.Float,
            double => TargetValueType.Double,
            decimal => TargetValueType.Decimal,
            long or ulong => TargetValueType.Long,
            byte or sbyte or short or ushort or int or uint => TargetValueType.Int,
            not null => TargetValueType.Object,
            _ => TargetValueType.Unknown
        };
    }

    /// <summary>
    /// Resolves the effective registry target value type from explicit metadata and runtime fallback information.
    /// </summary>
    /// <param name="declaredType">The explicit metadata type text.</param>
    /// <param name="targetType">The runtime type, if known.</param>
    /// <param name="sampleValue">The runtime sample value, if known.</param>
    /// <returns>The effective canonical target value type.</returns>
    public static TargetValueType Resolve(string? declaredType, Type? targetType, object? sampleValue)
    {
        var explicitType = Parse(declaredType);
        return explicitType != TargetValueType.Unknown
            ? explicitType
            : Infer(targetType, sampleValue);
    }

    /// <summary>
    /// Maps one canonical target value type to the SetValue target kind used by editors and validation.
    /// </summary>
    /// <param name="targetValueType">The canonical target value type.</param>
    /// <returns>The matching SetValue target kind.</returns>
    public static SetValueTargetKind ToSetValueTargetKind(TargetValueType targetValueType)
        => targetValueType switch
        {
            TargetValueType.String => SetValueTargetKind.String,
            TargetValueType.Bool => SetValueTargetKind.Boolean,
            TargetValueType.Int or TargetValueType.Long or TargetValueType.Float or TargetValueType.Double or TargetValueType.Decimal or TargetValueType.Epoch or TargetValueType.Bits => SetValueTargetKind.Numeric,
            _ => SetValueTargetKind.Unknown
        };

    /// <summary>
    /// Maps one canonical target value type to the preferred CLR type for value conversion.
    /// </summary>
    /// <param name="targetValueType">The canonical target value type.</param>
    /// <returns>The preferred CLR type, or <see langword="null"/> when no concrete conversion type is defined.</returns>
    public static Type? ToClrType(TargetValueType targetValueType)
        => targetValueType switch
        {
            TargetValueType.String => typeof(string),
            TargetValueType.Bool => typeof(bool),
            TargetValueType.Int => typeof(int),
            TargetValueType.Long => typeof(long),
            TargetValueType.Float => typeof(float),
            TargetValueType.Double => typeof(double),
            TargetValueType.Decimal => typeof(decimal),
            TargetValueType.Epoch => typeof(long),
            TargetValueType.Bits => typeof(ulong),
            _ => null
        };
}
