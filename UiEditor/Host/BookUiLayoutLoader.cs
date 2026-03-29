using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace Amium.Host;

public sealed class BookUiPageLayout
{
    public string PageName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Caption { get; init; } = string.Empty;
    public IReadOnlyDictionary<int, string> Views { get; init; } = new Dictionary<int, string>();
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
            Caption = title,
            DocumentProperties = ReadProperties(root, "Layout"),
            Layout = ReadNode(layoutElement)
        };
    }

    public static BookUiPageLayout LoadYaml(string uiFilePath, string fallbackPageName)
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

        using var reader = new StringReader(content);
        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException("Page.yaml does not contain a valid root mapping.");
        }

        var pageName = GetScalar(root, "Page") ?? fallbackPageName;
        var caption = GetScalar(root, "Caption") ?? GetScalar(root, "Title") ?? pageName;
        var views = ReadViews(root);
        var controls = GetSequence(root, "Controls");

        var children = new List<BookUiNode>();
        if (controls is not null)
        {
            foreach (var controlNode in controls.Children.OfType<YamlMappingNode>())
            {
                children.Add(ReadYamlControlNode(controlNode));
            }
        }

        var documentProperties = new JsonObject
        {
            ["Page"] = pageName,
            ["Title"] = caption,
            ["Caption"] = caption,
            ["Views"] = new JsonObject(views.Select(static entry => new KeyValuePair<string, JsonNode?>(entry.Key.ToString(CultureInfo.InvariantCulture), entry.Value)))
        };

        return new BookUiPageLayout
        {
            PageName = pageName,
            Title = caption,
            Caption = caption,
            Views = views,
            DocumentProperties = documentProperties,
            Layout = new BookUiNode
            {
                Type = "Canvas",
                Text = caption,
                Properties = new JsonObject
                {
                    ["Type"] = "Canvas"
                },
                Children = children
            }
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
            Caption = fallbackPageName,
            Views = new Dictionary<int, string>
            {
                [1] = "HomeScreen"
            },
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

    private static Dictionary<int, string> ReadViews(YamlMappingNode root)
    {
        var views = new Dictionary<int, string>();
        if (GetMapping(root, "Views") is not { } viewsNode)
        {
            views[1] = "HomeScreen";
            return views;
        }

        foreach (var entry in viewsNode.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode || entry.Value is not YamlScalarNode valueNode)
            {
                continue;
            }

            if (int.TryParse(keyNode.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var key))
            {
                views[key] = valueNode.Value ?? string.Empty;
            }
        }

        if (views.Count == 0)
        {
            views[1] = "HomeScreen";
        }

        return views;
    }

    private static BookUiNode ReadYamlControlNode(YamlMappingNode node)
    {
        var type = GetScalar(node, "Type") ?? string.Empty;
        var properties = new JsonObject
        {
            ["Type"] = type
        };

        SetPropertyIfPresent(properties, "View", GetScalarJsonNode(node, "View"));
        SetPropertyIfPresent(properties, "Enabled", GetScalarJsonNode(node, "Enabled"));

        if (GetMapping(node, "Identity") is { } identity)
        {
            SetPropertyIfPresent(properties, "Name", GetScalarJsonNode(identity, "Name"));
            SetPropertyIfPresent(properties, "Id", GetScalarJsonNode(identity, "Id"));
        }

        if (GetMapping(node, "Rect") is { } rect)
        {
            SetPropertyIfPresent(properties, "X", GetScalarJsonNode(rect, "X"));
            SetPropertyIfPresent(properties, "Y", GetScalarJsonNode(rect, "Y"));
            SetPropertyIfPresent(properties, "Width", GetScalarJsonNode(rect, "Width"));
            SetPropertyIfPresent(properties, "Height", GetScalarJsonNode(rect, "Height"));
        }

        if (GetMapping(node, "Design") is { } design)
        {
            SetPropertyIfPresent(properties, "CornerRadius", GetScalarJsonNode(design, "CornerRadius"));
            SetPropertyIfPresent(properties, "BorderWidth", GetScalarJsonNode(design, "BorderWidth"));
            SetPropertyIfPresent(properties, "BorderColor", GetScalarJsonNode(design, "BorderColor"));
            SetPropertyIfPresent(properties, "BackgroundColor", GetScalarJsonNode(design, "BackColor"));
            SetPropertyIfPresent(properties, "ToolTipText", GetScalarJsonNode(design, "ToolTip"));
        }

        if (GetMapping(node, "Header") is { } header)
        {
            SetPropertyIfPresent(properties, "ControlCaption", GetScalarJsonNode(header, "ControlCaption"));
            SetPropertyIfPresent(properties, "HeaderForeColor", GetScalarJsonNode(header, "HeaderForeColor"));
            SetPropertyIfPresent(properties, "CaptionVisible", GetScalarJsonNode(header, "CaptionVisible"));
            SetPropertyIfPresent(properties, "ShowCaption", GetScalarJsonNode(header, "CaptionVisible"));
            SetPropertyIfPresent(properties, "HeaderCornerRadius", GetScalarJsonNode(header, "HeaderCornerRadius"));
            SetPropertyIfPresent(properties, "HeaderBorderWidth", GetScalarJsonNode(header, "HeaderBorderWidth"));
            SetPropertyIfPresent(properties, "HeaderBorderColor", GetScalarJsonNode(header, "HeaderBorderColor"));
            SetPropertyIfPresent(properties, "HeaderBackColor", GetScalarJsonNode(header, "HeaderBackColor"));
        }

        if (GetMapping(node, "Body") is { } body)
        {
            SetPropertyIfPresent(properties, "BodyCaption", GetScalarJsonNode(body, "BodyCaption"));
            SetPropertyIfPresent(properties, "BodyCaptionPosition", GetScalarJsonNode(body, "BodyCaptionPosition"));
            SetPropertyIfPresent(properties, "BodyForeColor", GetScalarJsonNode(body, "BodyForeColor"));
            SetPropertyIfPresent(properties, "BodyCaptionVisible", GetScalarJsonNode(body, "BodyCaptionVisible"));
            SetPropertyIfPresent(properties, "ShowBodyCaption", GetScalarJsonNode(body, "BodyCaptionVisible"));
            SetPropertyIfPresent(properties, "BodyCornerRadius", GetScalarJsonNode(body, "BodyCornerRadius"));
            SetPropertyIfPresent(properties, "BodyBorderWidth", GetScalarJsonNode(body, "BodyBorderWidth"));
            SetPropertyIfPresent(properties, "BodyBorderColor", GetScalarJsonNode(body, "BodyBorderColor"));
            SetPropertyIfPresent(properties, "BodyBackColor", GetScalarJsonNode(body, "BodyBackColor"));
        }

        if (GetMapping(node, "Footer") is { } footer)
        {
            SetPropertyIfPresent(properties, "ShowFooter", GetScalarJsonNode(footer, "ShowFooter"));
            SetPropertyIfPresent(properties, "FooterCornerRadius", GetScalarJsonNode(footer, "FooterCornerRadius"));
            SetPropertyIfPresent(properties, "FooterBorderWidth", GetScalarJsonNode(footer, "FooterBorderWidth"));
            SetPropertyIfPresent(properties, "FooterBorderColor", GetScalarJsonNode(footer, "FooterBorderColor"));
            SetPropertyIfPresent(properties, "FooterBackColor", GetScalarJsonNode(footer, "FooterBackColor"));
        }

        if (GetSequence(node, "InteractionRules") is { } rules)
        {
            var array = new JsonArray();
            foreach (var ruleNode in rules.Children.OfType<YamlMappingNode>())
            {
                var rule = new JsonObject();
                SetPropertyIfPresent(rule, "Event", GetScalarJsonNode(ruleNode, "Event"));
                SetPropertyIfPresent(rule, "Action", GetScalarJsonNode(ruleNode, "Action"));
                SetPropertyIfPresent(rule, "TargetPath", GetScalarJsonNode(ruleNode, "TargetPath"));
                SetPropertyIfPresent(rule, "Argument", GetScalarJsonNode(ruleNode, "Argument"));
                array.Add(rule);
            }

            properties["InteractionRules"] = array;
        }

        var children = new List<BookUiNode>();
        if (GetMapping(node, "Control") is { } control)
        {
            ReadYamlControlProperties(type, control, properties, children);
        }

        var text = GetStringValue(properties, "BodyCaption")
            ?? GetStringValue(properties, "Name")
            ?? type;

        return new BookUiNode
        {
            Type = type,
            Text = text,
            X = GetDoubleValue(properties, "X"),
            Y = GetDoubleValue(properties, "Y"),
            Width = GetDoubleValue(properties, "Width"),
            Height = GetDoubleValue(properties, "Height"),
            Properties = properties,
            Children = children
        };
    }

    private static void ReadYamlControlProperties(string type, YamlMappingNode control, JsonObject properties, List<BookUiNode> children)
    {
        SetPropertyIfPresent(properties, "Unit", GetScalarJsonNode(control, "Unit"));
        SetPropertyIfPresent(properties, "TargetPath", GetScalarJsonNode(control, "TargetPath"));
        SetPropertyIfPresent(properties, "TargetParameterPath", GetScalarJsonNode(control, "TargetParameterPath"));
        SetPropertyIfPresent(properties, "TargetParameterFormat", GetScalarJsonNode(control, "TargetParameterFormat"));
        SetPropertyIfPresent(properties, "IsReadOnly", GetScalarJsonNode(control, "IsReadOnly"));
        SetPropertyIfPresent(properties, "RefreshRateMs", GetScalarJsonNode(control, "RefreshRateMs"));
        SetPropertyIfPresent(properties, "ButtonText", GetScalarJsonNode(control, "ButtonText"));
        SetPropertyIfPresent(properties, "ButtonIcon", GetScalarJsonNode(control, "ButtonIcon"));
        SetPropertyIfPresent(properties, "ButtonIconColor", GetScalarJsonNode(control, "ButtonIconColor"));
        SetPropertyIfPresent(properties, "ButtonBodyBackground", GetScalarJsonNode(control, "ButtonBackColor"));
        SetPropertyIfPresent(properties, "ButtonOnlyIcon", GetScalarJsonNode(control, "ButtonOnlyIcon"));
        SetPropertyIfPresent(properties, "ButtonIconAlign", GetScalarJsonNode(control, "ButtonIconAlign"));
        SetPropertyIfPresent(properties, "ButtonTextAlign", GetScalarJsonNode(control, "ButtonTextAlign"));
        SetPropertyIfPresent(properties, "TargetLog", GetScalarJsonNode(control, "TargetLog"));
        SetPropertyIfPresent(properties, "HistorySeconds", GetScalarJsonNode(control, "HistorySeconds"));
        SetPropertyIfPresent(properties, "ViewSeconds", GetScalarJsonNode(control, "ViewSeconds"));
        SetPropertyIfPresent(properties, "UdlClientHost", GetScalarJsonNode(control, "UdlClientHost"));
        SetPropertyIfPresent(properties, "UdlClientPort", GetScalarJsonNode(control, "UdlClientPort"));
        SetPropertyIfPresent(properties, "UdlClientAutoConnect", GetScalarJsonNode(control, "UdlClientAutoConnect"));
        SetPropertyIfPresent(properties, "UdlClientDebugLogging", GetScalarJsonNode(control, "UdlClientDebugLogging"));
        SetPropertyIfPresent(properties, "UdlAttachedItemPaths", GetScalarJsonNode(control, "UdlAttachedItemPaths"));
        SetPropertyIfPresent(properties, "ControlHeight", GetScalarJsonNode(control, "ControlHeight"));
        SetPropertyIfPresent(properties, "ListItemHeight", GetScalarJsonNode(control, "ListItemHeight"));
        SetPropertyIfPresent(properties, "Rows", GetScalarJsonNode(control, "Rows"));
        SetPropertyIfPresent(properties, "Columns", GetScalarJsonNode(control, "Columns"));

        if (GetSequence(control, "ChartSeriesDefinitions") is { } chartDefinitions)
        {
            var definitions = chartDefinitions.Children
                .OfType<YamlScalarNode>()
                .Select(static child => child.Value ?? string.Empty)
                .Where(static value => !string.IsNullOrWhiteSpace(value));
            properties["ChartSeriesDefinitions"] = string.Join(Environment.NewLine, definitions);
        }

        if (GetSequence(control, "Children") is { } directChildren)
        {
            foreach (var childNode in directChildren.Children.OfType<YamlMappingNode>())
            {
                children.Add(ReadYamlControlNode(childNode));
            }
        }

        if (string.Equals(type, "TableControl", StringComparison.OrdinalIgnoreCase)
            && GetSequence(control, "Cells") is { } cells)
        {
            foreach (var cellNode in cells.Children.OfType<YamlMappingNode>())
            {
                if (GetMapping(cellNode, "Child") is not { } childMapping)
                {
                    continue;
                }

                var child = ReadYamlControlNode(childMapping);
                var childProperties = child.Properties.DeepClone() as JsonObject ?? new JsonObject();
                SetPropertyIfPresent(childProperties, "TableCellRow", GetScalarJsonNode(cellNode, "Row"));
                SetPropertyIfPresent(childProperties, "TableCellColumn", GetScalarJsonNode(cellNode, "Column"));
                SetPropertyIfPresent(childProperties, "TableCellRowSpan", GetScalarJsonNode(cellNode, "RowSpan"));
                SetPropertyIfPresent(childProperties, "TableCellColumnSpan", GetScalarJsonNode(cellNode, "ColumnSpan"));

                children.Add(new BookUiNode
                {
                    Type = child.Type,
                    Text = child.Text,
                    X = child.X,
                    Y = child.Y,
                    Width = child.Width,
                    Height = child.Height,
                    Properties = childProperties,
                    Children = child.Children
                });
            }
        }
    }

    private static void SetPropertyIfPresent(JsonObject target, string propertyName, JsonNode? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    private static string? GetScalar(YamlMappingNode node, string key)
    {
        return TryGetChild(node, key, out var child) && child is YamlScalarNode scalar
            ? scalar.Value
            : null;
    }

    private static JsonNode? GetScalarJsonNode(YamlMappingNode node, string key)
    {
        if (!TryGetChild(node, key, out var child) || child is not YamlScalarNode scalar)
        {
            return null;
        }

        return ParseScalar(scalar.Value);
    }

    private static YamlMappingNode? GetMapping(YamlMappingNode node, string key)
    {
        return TryGetChild(node, key, out var child) ? child as YamlMappingNode : null;
    }

    private static YamlSequenceNode? GetSequence(YamlMappingNode node, string key)
    {
        return TryGetChild(node, key, out var child) ? child as YamlSequenceNode : null;
    }

    private static bool TryGetChild(YamlMappingNode node, string key, out YamlNode child)
    {
        foreach (var entry in node.Children)
        {
            if (entry.Key is YamlScalarNode keyNode && string.Equals(keyNode.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                child = entry.Value;
                return true;
            }
        }

        child = null!;
        return false;
    }

    private static JsonNode? ParseScalar(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (bool.TryParse(value, out var boolResult))
        {
            return JsonValue.Create(boolResult);
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intResult))
        {
            return JsonValue.Create(intResult);
        }

        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var doubleResult))
        {
            return JsonValue.Create(doubleResult);
        }

        return JsonValue.Create(value);
    }

    private static string? GetStringValue(JsonObject properties, string propertyName)
    {
        return properties[propertyName] switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            _ => null
        };
    }

    private static double? GetDoubleValue(JsonObject properties, string propertyName)
    {
        return properties[propertyName] switch
        {
            JsonValue value when value.TryGetValue<double>(out var number) => number,
            JsonValue value when value.TryGetValue<int>(out var intNumber) => intNumber,
            JsonValue value when value.TryGetValue<string>(out var text) && double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }
}
