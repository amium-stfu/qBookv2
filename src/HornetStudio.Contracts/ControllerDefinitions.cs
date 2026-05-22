using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HornetStudio.Editor.Models;

/// <summary>
/// Defines the supported controller kinds for the controller widget.
/// </summary>
public enum ControllerType
{
    PID
}

/// <summary>
/// Stores the persisted PID controller configuration.
/// </summary>
public sealed class PidControllerDefinition
{
    public double Ks { get; set; }

    public double Tu { get; set; }

    public double Tg { get; set; }

    public double DFilterTauMs { get; set; }

    public double SetMin { get; set; }

    public double SetMax { get; set; } = 100.0;

    public double OutMin { get; set; }

    public double OutMax { get; set; } = 100.0;

    public int ComputeIntervalMs { get; set; } = 100;

    public int OutputIntervalMs { get; set; } = 100;

    public PidControllerDefinition Clone()
    {
        return new PidControllerDefinition
        {
            Ks = Ks,
            Tu = Tu,
            Tg = Tg,
            DFilterTauMs = DFilterTauMs,
            SetMin = SetMin,
            SetMax = SetMax,
            OutMin = OutMin,
            OutMax = OutMax,
            ComputeIntervalMs = ComputeIntervalMs,
            OutputIntervalMs = OutputIntervalMs
        };
    }

    public PidControllerDefinition Normalize()
    {
        var normalized = Clone();
        normalized.ComputeIntervalMs = Math.Max(1, normalized.ComputeIntervalMs);
        normalized.OutputIntervalMs = Math.Max(1, normalized.OutputIntervalMs);

        if (normalized.SetMax < normalized.SetMin)
        {
            (normalized.SetMin, normalized.SetMax) = (normalized.SetMax, normalized.SetMin);
        }

        if (normalized.OutMax < normalized.OutMin)
        {
            (normalized.OutMin, normalized.OutMax) = (normalized.OutMax, normalized.OutMin);
        }

        return normalized;
    }
}

/// <summary>
/// Stores a persisted controller definition for the controller widget.
/// </summary>
public sealed class ControllerDefinition
{
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public ControllerType Type { get; set; } = ControllerType.PID;

    public string SourcePath { get; set; } = string.Empty;

    public string SetpointPath { get; set; } = string.Empty;

    public string OutputPath { get; set; } = string.Empty;

    public PidControllerDefinition Pid { get; set; } = new();

    public ControllerDefinition Clone()
    {
        return new ControllerDefinition
        {
            Name = Name,
            Enabled = Enabled,
            Type = Type,
            SourcePath = SourcePath,
            SetpointPath = SetpointPath,
            OutputPath = OutputPath,
            Pid = Pid.Clone()
        };
    }

    public ControllerDefinition Normalize()
    {
        var normalized = Clone();
        normalized.Name = normalized.Name.Trim();
        normalized.SourcePath = NormalizePath(normalized.SourcePath);
        normalized.SetpointPath = NormalizePath(normalized.SetpointPath);
        normalized.OutputPath = NormalizePath(normalized.OutputPath);
        normalized.Pid = normalized.Pid.Normalize();
        normalized.Type = ControllerType.PID;
        return normalized;
    }

    private static string NormalizePath(string? path)
    {
        return path?.Trim() ?? string.Empty;
    }
}

/// <summary>
/// Converts controller definitions between raw JSON and normalized shared models.
/// </summary>
public static class ControllerDefinitionJsonCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<ControllerDefinition> ParseDefinitions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<ControllerDefinition>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<ControllerDefinition>>(raw, JsonOptions);
            return parsed?
                .Where(static definition => definition is not null && !string.IsNullOrWhiteSpace(definition.Name))
                .Select(static definition => definition!.Normalize())
                .ToArray()
                ?? Array.Empty<ControllerDefinition>();
        }
        catch
        {
            return Array.Empty<ControllerDefinition>();
        }
    }

    public static string SerializeDefinitions(IEnumerable<ControllerDefinition>? definitions)
    {
        var normalized = definitions?
            .Where(static definition => definition is not null)
            .Select(static definition => definition!.Normalize())
            .Where(static definition => !string.IsNullOrWhiteSpace(definition.Name))
            .ToArray()
            ?? Array.Empty<ControllerDefinition>();

        return normalized.Length == 0
            ? string.Empty
            : JsonSerializer.Serialize(normalized, JsonOptions);
    }
}