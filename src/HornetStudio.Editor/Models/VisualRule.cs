using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HornetStudio.Editor.Models;

/// <summary>
/// Defines the supported source kinds for widget visual rules.
/// </summary>
public enum VisualRuleSourceKind
{
    MonitorRule
}

/// <summary>
/// Defines the supported widget sections that can be styled by a visual rule.
/// </summary>
public enum VisualRuleTarget
{
    Header,
    Body,
    Footer,
    Widget
}

/// <summary>
/// Defines the supported visual properties for widget visual rules.
/// </summary>
[JsonConverter(typeof(VisualRulePropertyJsonConverter))]
public enum VisualRuleProperty
{
    BodyBackColor,
    ButtonBackColor,
    DisplayBackColor
}

/// <summary>
/// Defines the supported visual effects for widget visual rules.
/// </summary>
public enum VisualRuleEffect
{
    None,
    Blink
}

/// <summary>
/// Represents a persisted widget visual rule definition.
/// </summary>
public sealed class VisualRule
{
    /// <summary>
    /// Gets the source kind that drives the rule.
    /// </summary>
    public VisualRuleSourceKind SourceKind { get; init; } = VisualRuleSourceKind.MonitorRule;

    /// <summary>
    /// Gets the monitor rule source path.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target section that is affected by the rule.
    /// </summary>
    public VisualRuleTarget Target { get; init; } = VisualRuleTarget.Body;

    /// <summary>
    /// Gets the visual property that is affected by the rule.
    /// </summary>
    public VisualRuleProperty Property { get; init; } = VisualRuleProperty.BodyBackColor;

    /// <summary>
    /// Gets the active-state value that is applied while the rule is active.
    /// </summary>
    public string ActiveValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional inactive-state value that is applied while the rule is inactive.
    /// </summary>
    public string InactiveValue { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UI-only effect that is applied while the rule is active.
    /// </summary>
    public VisualRuleEffect Effect { get; init; } = VisualRuleEffect.None;
}

/// <summary>
/// Provides parsing and serialization for persisted widget visual rules.
/// </summary>
public static class VisualRuleCodec
{
    private static readonly IReadOnlyList<string> SupportedSignalProperties = [nameof(VisualRuleProperty.BodyBackColor)];
    private static readonly IReadOnlyList<string> SupportedButtonProperties = [nameof(VisualRuleProperty.ButtonBackColor)];
    private static readonly IReadOnlyList<string> SupportedCircleDisplayProperties = [nameof(VisualRuleProperty.DisplayBackColor)];

    /// <summary>
    /// Gets the available source kind option names.
    /// </summary>
    public static IReadOnlyList<string> SourceKindOptions { get; } = Enum.GetNames<VisualRuleSourceKind>();

    /// <summary>
    /// Gets the available target option names.
    /// </summary>
    public static IReadOnlyList<string> TargetOptions { get; } = Enum.GetNames<VisualRuleTarget>();

    /// <summary>
    /// Gets the available property option names.
    /// </summary>
    public static IReadOnlyList<string> PropertyOptions { get; } = Enum.GetNames<VisualRuleProperty>();

    /// <summary>
    /// Gets the available effect option names.
    /// </summary>
    public static IReadOnlyList<string> EffectOptions { get; } = Enum.GetNames<VisualRuleEffect>();

    /// <summary>
    /// Gets the supported visual property options for the specified item.
    /// </summary>
    public static IReadOnlyList<string> GetPropertyOptions(FolderItemModel? item)
        => item switch
        {
            { IsItem: true } => SupportedSignalProperties,
            { IsButton: true } => SupportedButtonProperties,
            { IsCircleDisplay: true } => SupportedCircleDisplayProperties,
            _ => []
        };

    /// <summary>
    /// Determines whether the specified item supports visual rules.
    /// </summary>
    public static bool SupportsVisualRules(FolderItemModel? item)
        => GetPropertyOptions(item).Count > 0;

    /// <summary>
    /// Determines whether the specified property is supported for the specified item.
    /// </summary>
    public static bool IsSupportedProperty(FolderItemModel? item, VisualRuleProperty property)
        => GetPropertyOptions(item).Contains(property.ToString(), StringComparer.Ordinal);

    /// <summary>
    /// Gets the compatibility target persisted for the specified visual property.
    /// </summary>
    public static VisualRuleTarget GetCompatibilityTarget(VisualRuleProperty property)
        => property switch
        {
            VisualRuleProperty.BodyBackColor => VisualRuleTarget.Body,
            VisualRuleProperty.ButtonBackColor => VisualRuleTarget.Widget,
            VisualRuleProperty.DisplayBackColor => VisualRuleTarget.Widget,
            _ => VisualRuleTarget.Body
        };

    /// <summary>
    /// Parses a persisted visual property token with legacy compatibility.
    /// </summary>
    public static bool TryParsePersistedProperty(string? value, VisualRuleTarget target, out VisualRuleProperty property)
    {
        if (Enum.TryParse(value, ignoreCase: true, out property))
        {
            return true;
        }

        if (string.Equals(value?.Trim(), "Background", StringComparison.OrdinalIgnoreCase)
            && target == VisualRuleTarget.Body)
        {
            property = VisualRuleProperty.BodyBackColor;
            return true;
        }

        property = default;
        return false;
    }

    /// <summary>
    /// Parses persisted visual rule definitions from their line-based representation.
    /// </summary>
    public static List<VisualRule> ParseDefinitions(string? raw)
    {
        var rules = new List<VisualRule>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return rules;
        }

        var lines = raw
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (!TryParseTarget(parts.Length > 2 ? parts[2] : null, out var target)
                || !TryParsePersistedProperty(parts.Length > 3 ? parts[3] : null, target, out var property))
            {
                continue;
            }

            if (parts.Length < 5
                || !Enum.TryParse<VisualRuleSourceKind>(parts[0], ignoreCase: true, out var sourceKind)
                || !Enum.TryParse<VisualRuleEffect>(parts[4], ignoreCase: true, out var effect))
            {
                continue;
            }

            rules.Add(new VisualRule
            {
                SourceKind = sourceKind,
                SourcePath = parts[1].Trim(),
                Target = target,
                Property = property,
                Effect = effect,
                ActiveValue = parts.Length > 5 ? parts[5].Trim() : string.Empty,
                InactiveValue = parts.Length > 6 ? parts[6].Trim() : string.Empty
            });
        }

        return rules;
    }

    /// <summary>
    /// Serializes visual rules to their line-based representation.
    /// </summary>
    public static string SerializeDefinitions(IEnumerable<VisualRule> rules)
        => string.Join(Environment.NewLine, rules.Select(SerializeDefinition).Where(static line => !string.IsNullOrWhiteSpace(line)));

    private static string SerializeDefinition(VisualRule rule)
    {
        var values = new List<string>
        {
            rule.SourceKind.ToString(),
            Sanitize(rule.SourcePath, string.Empty),
            GetCompatibilityTarget(rule.Property).ToString(),
            rule.Property.ToString(),
            rule.Effect.ToString(),
            Sanitize(rule.ActiveValue, string.Empty),
            Sanitize(rule.InactiveValue, string.Empty)
        };

        return string.Join("|", values);
    }

    private static string Sanitize(string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "/", StringComparison.Ordinal);
    }

    private static bool TryParseTarget(string? value, out VisualRuleTarget target)
        => Enum.TryParse(value, ignoreCase: true, out target);

}

/// <summary>
/// Provides JSON compatibility for persisted visual rule property values.
/// </summary>
public sealed class VisualRulePropertyJsonConverter : JsonConverter<VisualRuleProperty>
{
    /// <inheritdoc />
    public override VisualRuleProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (VisualRuleCodec.TryParsePersistedProperty(value, VisualRuleTarget.Body, out var property))
            {
                return property;
            }
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue)
            && Enum.IsDefined(typeof(VisualRuleProperty), numericValue))
        {
            return (VisualRuleProperty)numericValue;
        }

        return VisualRuleProperty.BodyBackColor;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, VisualRuleProperty value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}