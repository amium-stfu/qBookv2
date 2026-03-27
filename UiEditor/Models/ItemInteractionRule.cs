using System;
using System.Collections.Generic;
using System.Linq;

namespace Amium.UiEditor.Models;

public enum ItemInteractionEvent
{
    BodyLeftClick,
    BodyRightClick
}

public enum ItemInteractionAction
{
    OpenValueEditor,
    ToggleBool,
    SetValue,
    SendInputTo
}

public sealed class ItemInteractionRule
{
    public ItemInteractionEvent Event { get; init; } = ItemInteractionEvent.BodyLeftClick;

    public ItemInteractionAction Action { get; init; } = ItemInteractionAction.OpenValueEditor;

    public string TargetPath { get; init; } = "this";

    public string Argument { get; init; } = string.Empty;
}

public static class ItemInteractionRuleCodec
{
    public static IReadOnlyList<string> EventOptions { get; } = Enum.GetNames<ItemInteractionEvent>();

    public static IReadOnlyList<string> ActionOptions { get; } = Enum.GetNames<ItemInteractionAction>();

    public static List<ItemInteractionRule> ParseDefinitions(string? raw)
    {
        var rules = new List<ItemInteractionRule>();
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
            if (parts.Length < 2
                || !Enum.TryParse<ItemInteractionEvent>(parts[0], ignoreCase: true, out var eventKind)
                || !Enum.TryParse<ItemInteractionAction>(parts[1], ignoreCase: true, out var actionKind))
            {
                continue;
            }

            var targetPath = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2])
                ? parts[2].Trim()
                : "this";
            var argument = parts.Length > 3 ? parts[3].Trim() : string.Empty;

            rules.Add(new ItemInteractionRule
            {
                Event = eventKind,
                Action = actionKind,
                TargetPath = targetPath,
                Argument = argument
            });
        }

        return rules;
    }

    public static string SerializeDefinitions(IEnumerable<ItemInteractionRule> rules)
        => string.Join(Environment.NewLine, rules.Select(SerializeDefinition).Where(static line => !string.IsNullOrWhiteSpace(line)));

    private static string SerializeDefinition(ItemInteractionRule rule)
        => string.Join("|",
            rule.Event.ToString(),
            rule.Action.ToString(),
            Sanitize(rule.TargetPath, "this"),
            Sanitize(rule.Argument, string.Empty));

    private static string Sanitize(string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "/", StringComparison.Ordinal);
    }
}