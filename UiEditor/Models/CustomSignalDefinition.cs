using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Amium.UiEditor.Helpers;

namespace Amium.UiEditor.Models;

public enum CustomSignalMode
{
    Input,
    Constant,
    Computed
}

public enum CustomSignalDataType
{
    Number,
    Boolean,
    Text
}

public enum CustomSignalOperation
{
    Copy,
    Add,
    Subtract,
    Multiply,
    Divide,
    Min,
    Max,
    GreaterThan,
    LessThan,
    Equals,
    And,
    Or,
    Concat,
    If
}

public sealed class CustomSignalDefinition
{
    public string Name { get; set; } = string.Empty;

    public CustomSignalMode Mode { get; set; } = CustomSignalMode.Input;

    public CustomSignalDataType DataType { get; set; } = CustomSignalDataType.Number;

    public bool IsWritable { get; set; } = true;

    public string Unit { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string ValueText { get; set; } = string.Empty;

    public CustomSignalOperation Operation { get; set; } = CustomSignalOperation.Copy;

    public string SourcePath { get; set; } = string.Empty;

    public string SourcePath2 { get; set; } = string.Empty;

    public string SourcePath3 { get; set; } = string.Empty;

    public CustomSignalDefinition Clone()
    {
        return new CustomSignalDefinition
        {
            Name = Name,
            Mode = Mode,
            DataType = DataType,
            IsWritable = IsWritable,
            Unit = Unit,
            Format = Format,
            ValueText = ValueText,
            Operation = Operation,
            SourcePath = SourcePath,
            SourcePath2 = SourcePath2,
            SourcePath3 = SourcePath3
        };
    }
}

public sealed class CustomSignalDefinitionDocument
{
    public string Name { get; init; } = string.Empty;

    public CustomSignalMode Mode { get; init; } = CustomSignalMode.Input;

    public CustomSignalDataType DataType { get; init; } = CustomSignalDataType.Number;

    public bool IsWritable { get; init; } = true;

    public string Unit { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public string ValueText { get; init; } = string.Empty;

    public CustomSignalOperation Operation { get; init; } = CustomSignalOperation.Copy;

    public string SourcePath { get; init; } = string.Empty;

    public string SourcePath2 { get; init; } = string.Empty;

    public string SourcePath3 { get; init; } = string.Empty;
}

public static class CustomSignalDefinitionCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<CustomSignalDefinition> ParseDefinitions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<CustomSignalDefinition>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<CustomSignalDefinition>>(raw, JsonOptions);
            return parsed?
                .Where(static definition => definition is not null)
                .Select(static definition => definition!)
                .ToArray()
                ?? Array.Empty<CustomSignalDefinition>();
        }
        catch
        {
            return Array.Empty<CustomSignalDefinition>();
        }
    }

    public static string SerializeDefinitions(IEnumerable<CustomSignalDefinition>? definitions)
    {
        var normalized = definitions?
            .Where(static definition => definition is not null)
            .Select(static definition => definition!)
            .Where(static definition => !string.IsNullOrWhiteSpace(definition.Name))
            .ToArray()
            ?? Array.Empty<CustomSignalDefinition>();

        return normalized.Length == 0
            ? string.Empty
            : JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static List<CustomSignalDefinitionDocument> ToDocuments(string? rawDefinitions, string? folderName)
    {
        return ParseDefinitions(rawDefinitions)
            .Select(definition => ToDocument(definition, folderName))
            .ToList();
    }

    public static string FromDocuments(IEnumerable<CustomSignalDefinitionDocument>? documents, string? folderName)
    {
        if (documents is null)
        {
            return string.Empty;
        }

        var definitions = documents
            .Where(static document => document is not null)
            .Select(document => FromDocument(document!, folderName))
            .ToArray();

        return SerializeDefinitions(definitions);
    }

    public static JsonArray ToJsonArray(string? rawDefinitions, string? folderName)
    {
        var array = new JsonArray();
        foreach (var definition in ParseDefinitions(rawDefinitions))
        {
            array.Add(JsonSerializer.SerializeToNode(ToDocument(definition, folderName), JsonOptions));
        }

        return array;
    }

    public static string FromJsonNode(JsonNode? node, string? folderName)
    {
        if (node is JsonArray array)
        {
            var definitions = array
                .Select(child => child?.Deserialize<CustomSignalDefinitionDocument>(JsonOptions))
                .Where(static document => document is not null)
                .Select(document => FromDocument(document!, folderName));

            return SerializeDefinitions(definitions);
        }

        return node?.GetValue<string>() ?? string.Empty;
    }

    private static CustomSignalDefinitionDocument ToDocument(CustomSignalDefinition definition, string? folderName)
    {
        return new CustomSignalDefinitionDocument
        {
            Name = definition.Name,
            Mode = definition.Mode,
            DataType = definition.DataType,
            IsWritable = definition.IsWritable,
            Unit = definition.Unit,
            Format = definition.Format,
            ValueText = definition.ValueText,
            Operation = definition.Operation,
            SourcePath = ToPersistedTargetPath(definition.SourcePath, folderName),
            SourcePath2 = ToPersistedTargetPath(definition.SourcePath2, folderName),
            SourcePath3 = ToPersistedTargetPath(definition.SourcePath3, folderName)
        };
    }

    private static CustomSignalDefinition FromDocument(CustomSignalDefinitionDocument document, string? folderName)
    {
        return new CustomSignalDefinition
        {
            Name = document.Name,
            Mode = document.Mode,
            DataType = document.DataType,
            IsWritable = document.IsWritable,
            Unit = document.Unit,
            Format = document.Format,
            ValueText = document.ValueText,
            Operation = document.Operation,
            SourcePath = NormalizeTargetPath(document.SourcePath, folderName),
            SourcePath2 = NormalizeTargetPath(document.SourcePath2, folderName),
            SourcePath3 = NormalizeTargetPath(document.SourcePath3, folderName)
        };
    }

    private static string ToPersistedTargetPath(string? value, string? folderName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : TargetPathHelper.ToPersistedLayoutTargetPath(value, folderName);
    }

    private static string NormalizeTargetPath(string? value, string? folderName)
    {
        _ = folderName;
        var normalized = TargetPathHelper.NormalizeConfiguredTargetPath(value ?? string.Empty);
        return CollapseLegacyCustomSignalPath(normalized);
    }

    private static string CollapseLegacyCustomSignalPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var segments = value
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5
            || !string.Equals(segments[0], "Project", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[2], "CustomSignals", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return string.Join('.', segments.Where((_, index) => index != 3));
    }
}