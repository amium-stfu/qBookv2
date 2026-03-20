using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UiEditor.Host;

public sealed class BookUiPageLayout
{
    public string PageName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public JsonObject DocumentProperties { get; init; } = [];
    public BookUiNode Layout { get; init; } = new();
}

public sealed class BookUiNode
{
    public string Type { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
    public double? Spacing { get; init; }
    public JsonObject Properties { get; init; } = [];
    public IReadOnlyList<BookUiNode> Children { get; init; } = [];
}

public static class BookUiLayoutLoader
{
    public static BookUiPageLayout Load(string uiFilePath, string fallbackPageName)
    {
        if (string.IsNullOrWhiteSpace(uiFilePath))
        {
            throw new ArgumentException("UI file path must not be empty.", nameof(uiFilePath));
        }

        if (!File.Exists(uiFilePath))
        {
            throw new FileNotFoundException("UI file not found.", uiFilePath);
        }

        var content = File.ReadAllText(uiFilePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return CreateEmptyLayout(fallbackPageName);
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var pageName = GetString(root, "Page") ?? fallbackPageName;
        var title = GetString(root, "Title") ?? pageName;

        if (!root.TryGetProperty("Layout", out var layoutElement))
        {
            throw new InvalidDataException("Page.json does not contain a Layout node.");
        }

        return new BookUiPageLayout
        {
            PageName = pageName,
            Title = title,
            DocumentProperties = ReadProperties(root, "Layout"),
            Layout = ReadNode(layoutElement)
        };
    }

    private static BookUiNode ReadNode(JsonElement element)
    {
        var children = new List<BookUiNode>();
        if (element.TryGetProperty("Children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                children.Add(ReadNode(child));
            }
        }

        var properties = ReadProperties(element, "Children");
        return new BookUiNode
        {
            Type = GetString(properties, "Type") ?? string.Empty,
            Text = GetString(properties, "Text") ?? string.Empty,
            X = GetDouble(properties, "X"),
            Y = GetDouble(properties, "Y"),
            Width = GetDouble(properties, "Width"),
            Height = GetDouble(properties, "Height"),
            Spacing = GetDouble(properties, "Spacing"),
            Properties = properties,
            Children = children
        };
    }

    private static JsonObject ReadProperties(JsonElement element, params string[] excludedProperties)
    {
        var excluded = new HashSet<string>(excludedProperties, StringComparer.OrdinalIgnoreCase);
        var properties = new JsonObject();
        foreach (var property in element.EnumerateObject())
        {
            if (excluded.Contains(property.Name))
            {
                continue;
            }

            properties[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }

        return properties;
    }

    private static BookUiPageLayout CreateEmptyLayout(string fallbackPageName)
    {
        return new BookUiPageLayout
        {
            PageName = fallbackPageName,
            Title = fallbackPageName,
            Layout = new BookUiNode
            {
                Type = "Canvas",
                Properties = new JsonObject
                {
                    ["Type"] = "Canvas"
                },
                Children = []
            }
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetString(JsonObject properties, string propertyName)
    {
        return properties[propertyName]?.GetValue<string>();
    }

    private static double? GetDouble(JsonObject properties, string propertyName)
    {
        var value = properties[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<double>(out var result) => result,
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) && double.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }
}
