using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using Amium.Host;
using Amium.Items;
using Amium.UiEditor.Models;

namespace Amium.UiEditor.ViewModels;

public sealed class EditorDialogField : ObservableObject
{
    public EditorDialogField(EditorDialogBindingDefinition definition, Parameter parameter)
    {
        Definition = definition;
        Parameter = parameter;

        foreach (var axis in new[] { "Y1", "Y2", "Y3", "Y4" })
        {
            ChartAxisOptions.Add(axis);
        }

        foreach (var style in new[] { "Line", "Step" })
        {
            ChartStyleOptions.Add(style);
        }

        foreach (var eventOption in ItemInteractionRuleCodec.EventOptions)
        {
            InteractionEventOptions.Add(eventOption);
        }

        foreach (var actionOption in ItemInteractionRuleCodec.ActionOptions)
        {
            InteractionActionOptions.Add(actionOption);
        }
    }

    public EditorDialogBindingDefinition Definition { get; }

    public Parameter Parameter { get; }

    public string Key => Definition.Key;

    public string Label => Definition.Label;

    public EditorPropertyType PropertyType => Definition.PropertyType;

    public bool IsReadOnly => Definition.IsReadOnly;

    public ObservableCollection<string> Options { get; } = [];

    public ObservableCollection<ChartSeriesEditorRow> ChartSeriesEntries { get; } = [];

    public ObservableCollection<AttachItemEditorRow> AttachItemEntries { get; } = [];

    public ObservableCollection<ItemInteractionEditorRow> InteractionRuleEntries { get; } = [];

    public ObservableCollection<string> ChartTargetOptions { get; } = [];

    public ObservableCollection<string> ChartAxisOptions { get; } = [];

    public ObservableCollection<string> ChartStyleOptions { get; } = [];

    public ObservableCollection<string> InteractionEventOptions { get; } = [];

    public ObservableCollection<string> InteractionActionOptions { get; } = [];

    public ObservableCollection<string> InteractionTargetOptions { get; } = [];

    private string _toolTipText = string.Empty;
    private string _newChartTargetPath = string.Empty;
    private string _newChartAxis = "Y1";
    private string _newChartStyle = "Line";
    private string _newInteractionEventName = "BodyLeftClick";
    private string _newInteractionActionName = "OpenValueEditor";
    private string _newInteractionTargetPath = "this";
    private string _newInteractionArgument = string.Empty;

    public string ToolTipText
    {
        get => _toolTipText;
        set
        {
            if (!SetProperty(ref _toolTipText, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(ToolTipContent));
        }
    }

    public string Value
    {
        get => Parameter.Value?.ToString() ?? string.Empty;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(Value, normalized, StringComparison.Ordinal))
            {
                return;
            }

            Parameter.Value = normalized;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(PreviewColor));
            RaisePropertyChanged(nameof(PreviewBrush));
            RaisePropertyChanged(nameof(StructuredEditorSummary));

            if (IsChartSeriesList)
            {
                RebuildChartSeriesEntries();
            }

            if (IsInteractionRuleList)
            {
                RebuildInteractionRuleEntries();
            }
        }
    }

    public string NewChartTargetPath
    {
        get => _newChartTargetPath;
        set => SetProperty(ref _newChartTargetPath, value ?? string.Empty);
    }

    public string NewChartAxis
    {
        get => _newChartAxis;
        set => SetProperty(ref _newChartAxis, string.IsNullOrWhiteSpace(value) ? "Y1" : value);
    }

    public string NewChartStyle
    {
        get => _newChartStyle;
        set => SetProperty(ref _newChartStyle, NormalizeStyle(value));
    }

    public string NewInteractionEventName
    {
        get => _newInteractionEventName;
        set => SetProperty(ref _newInteractionEventName, string.IsNullOrWhiteSpace(value) ? "BodyLeftClick" : value);
    }

    public string NewInteractionActionName
    {
        get => _newInteractionActionName;
        set => SetProperty(ref _newInteractionActionName, string.IsNullOrWhiteSpace(value) ? "OpenValueEditor" : value);
    }

    public string NewInteractionTargetPath
    {
        get => _newInteractionTargetPath;
        set => SetProperty(ref _newInteractionTargetPath, string.IsNullOrWhiteSpace(value) ? "this" : value);
    }

    public string NewInteractionArgument
    {
        get => _newInteractionArgument;
        set => SetProperty(ref _newInteractionArgument, value ?? string.Empty);
    }

    public bool IsChoice => PropertyType == EditorPropertyType.Choice;

    public bool IsColor => PropertyType == EditorPropertyType.Color;

    public bool IsMultilineText => PropertyType == EditorPropertyType.MultilineText;

    public bool IsChartSeriesList => PropertyType == EditorPropertyType.ChartSeriesList;

    public bool IsAttachItemList => PropertyType == EditorPropertyType.AttachItemList;

    public bool IsInteractionRuleList => PropertyType == EditorPropertyType.InteractionRuleList;

    public bool IsTextInput => !IsChoice && !IsReadOnly && !IsMultilineText && !IsChartSeriesList && !IsAttachItemList && !IsInteractionRuleList;

    public bool ShowPickerButton => IsColor && !IsReadOnly;

    public string PreviewColor => string.IsNullOrWhiteSpace(Value) ? "Transparent" : Value;

    public object? ToolTipContent => string.IsNullOrWhiteSpace(ToolTipText) ? null : ToolTipText;

    public bool HasToolTipText => !string.IsNullOrWhiteSpace(ToolTipText);

    public string? InputWatermark => IsColor ? "Theme" : null;

    public string StructuredEditorSummary => PropertyType switch
    {
        EditorPropertyType.ChartSeriesList => ChartSeriesEntries.Count == 0
            ? "No series configured"
            : $"{ChartSeriesEntries.Count} series configured",
        EditorPropertyType.AttachItemList => Options.Count == 0
            ? "No items available"
            : $"{AttachItemEntries.Count(static row => row.IsAttached)} of {Options.Count} items attached",
        EditorPropertyType.InteractionRuleList => InteractionRuleEntries.Count == 0
            ? "Default left click opens value editor"
            : $"{InteractionRuleEntries.Count} rules configured",
        _ => string.Empty
    };

    public bool IsIconPathSelector => string.Equals(Key, "ButtonIcon", StringComparison.Ordinal);

    public IBrush PreviewBrush
    {
        get
        {
            if (!IsColor || string.IsNullOrWhiteSpace(Value))
            {
                return Brushes.Transparent;
            }

            return Color.TryParse(Value, out var color)
                ? new SolidColorBrush(color)
                : Brushes.Transparent;
        }
    }

    public void InitializeChartSeriesEditor()
    {
        if (!IsChartSeriesList)
        {
            return;
        }

        ChartTargetOptions.Clear();
        foreach (var target in HostRegistries.Data.GetAllKeys().OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            ChartTargetOptions.Add(target);
        }

        if (string.IsNullOrWhiteSpace(NewChartTargetPath) && ChartTargetOptions.Count > 0)
        {
            NewChartTargetPath = ChartTargetOptions[0];
        }

        RebuildChartSeriesEntries();
        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    public void InitializeAttachItemEditor()
    {
        if (!IsAttachItemList)
        {
            return;
        }

        RebuildAttachItemEntries();
        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    public void InitializeInteractionRuleEditor()
    {
        if (!IsInteractionRuleList)
        {
            return;
        }

        RefreshInteractionRuleTargetOptions(HostRegistries.Data.GetAllKeys().OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
    }

    public void RefreshAttachItemOptions(IEnumerable<string> options)
    {
        if (!IsAttachItemList)
        {
            return;
        }

        Options.Clear();
        foreach (var option in options.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Options.Add(option);
        }

        RebuildAttachItemEntries();
    }

    public void RefreshInteractionRuleTargetOptions(IEnumerable<string> options)
    {
        if (!IsInteractionRuleList)
        {
            return;
        }

        InteractionTargetOptions.Clear();
        InteractionTargetOptions.Add("this");
        foreach (var option in options.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            InteractionTargetOptions.Add(option);
        }

        if (string.IsNullOrWhiteSpace(NewInteractionTargetPath))
        {
            NewInteractionTargetPath = "this";
        }

        RebuildInteractionRuleEntries();
    }

    public void AddChartSeriesEntry()
    {
        if (!IsChartSeriesList || string.IsNullOrWhiteSpace(NewChartTargetPath))
        {
            return;
        }

        var row = CreateChartSeriesEntry(NewChartTargetPath, NewChartAxis, NewChartStyle);
        ChartSeriesEntries.Add(row);
        SyncChartSeriesValueFromEntries();
    }

    public List<ChartSeriesEditorRow> CreateChartSeriesSnapshot()
        => ChartSeriesEntries.Select(CloneChartSeriesEntry).ToList();

    public void ApplyChartSeriesEntries(IEnumerable<ChartSeriesEditorRow> rows)
    {
        if (!IsChartSeriesList)
        {
            return;
        }

        foreach (var row in ChartSeriesEntries)
        {
            row.PropertyChanged -= OnChartSeriesRowPropertyChanged;
        }

        ChartSeriesEntries.Clear();
        foreach (var row in rows.Where(static row => !string.IsNullOrWhiteSpace(row.TargetPath)).Select(static row => row))
        {
            ChartSeriesEntries.Add(CreateChartSeriesEntry(row.TargetPath, row.Axis, row.Style));
        }

        SyncChartSeriesValueFromEntries();
        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    public void RemoveChartSeriesEntry(ChartSeriesEditorRow row)
    {
        if (!IsChartSeriesList)
        {
            return;
        }

        row.PropertyChanged -= OnChartSeriesRowPropertyChanged;
        ChartSeriesEntries.Remove(row);
        SyncChartSeriesValueFromEntries();
    }

    public List<ItemInteractionEditorRow> CreateInteractionRuleSnapshot()
        => InteractionRuleEntries.Select(CloneInteractionRuleEntry).ToList();

    public void ApplyInteractionRuleEntries(IEnumerable<ItemInteractionEditorRow> rows)
    {
        if (!IsInteractionRuleList)
        {
            return;
        }

        foreach (var row in InteractionRuleEntries)
        {
            row.PropertyChanged -= OnInteractionRuleRowPropertyChanged;
        }

        InteractionRuleEntries.Clear();
        foreach (var row in rows)
        {
            InteractionRuleEntries.Add(CreateInteractionRuleEntry(row.EventName, row.ActionName, row.TargetPath, row.Argument));
        }

        SyncInteractionRuleValueFromEntries();
        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    public void RemoveInteractionRuleEntry(ItemInteractionEditorRow row)
    {
        if (!IsInteractionRuleList)
        {
            return;
        }

        row.PropertyChanged -= OnInteractionRuleRowPropertyChanged;
        InteractionRuleEntries.Remove(row);
        SyncInteractionRuleValueFromEntries();
    }

    private void RebuildChartSeriesEntries()
    {
        foreach (var row in ChartSeriesEntries)
        {
            row.PropertyChanged -= OnChartSeriesRowPropertyChanged;
        }

        ChartSeriesEntries.Clear();
        var lines = Value.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var targetPath = parts.Length > 0 ? parts[0] : string.Empty;
            var axis = parts.Length > 1 ? NormalizeAxis(parts[1]) : "Y1";
            var style = parts.Length > 2 ? NormalizeStyle(parts[2]) : "Line";
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                continue;
            }

            ChartSeriesEntries.Add(CreateChartSeriesEntry(targetPath, axis, style));
        }

        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    private void RebuildInteractionRuleEntries()
    {
        foreach (var row in InteractionRuleEntries)
        {
            row.PropertyChanged -= OnInteractionRuleRowPropertyChanged;
        }

        InteractionRuleEntries.Clear();
        foreach (var rule in ItemInteractionRuleCodec.ParseDefinitions(Value))
        {
            InteractionRuleEntries.Add(CreateInteractionRuleEntry(rule.Event.ToString(), rule.Action.ToString(), rule.TargetPath, rule.Argument));
        }

        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    private ChartSeriesEditorRow CreateChartSeriesEntry(string targetPath, string axis, string style)
    {
        var row = new ChartSeriesEditorRow
        {
            TargetPath = targetPath,
            Axis = NormalizeAxis(axis),
            Style = NormalizeStyle(style)
        };

        foreach (var option in ChartTargetOptions)
        {
            row.TargetOptions.Add(option);
        }

        foreach (var option in ChartAxisOptions)
        {
            row.AxisOptions.Add(option);
        }

        foreach (var option in ChartStyleOptions)
        {
            row.StyleOptions.Add(option);
        }

        row.PropertyChanged += OnChartSeriesRowPropertyChanged;
        return row;
    }

    private ItemInteractionEditorRow CreateInteractionRuleEntry(string eventName, string actionName, string targetPath, string argument)
    {
        var row = new ItemInteractionEditorRow
        {
            EventName = string.IsNullOrWhiteSpace(eventName) ? "BodyLeftClick" : eventName,
            ActionName = string.IsNullOrWhiteSpace(actionName) ? "OpenValueEditor" : actionName,
            TargetPath = string.IsNullOrWhiteSpace(targetPath) ? "this" : targetPath,
            Argument = argument ?? string.Empty
        };

        foreach (var option in InteractionEventOptions)
        {
            row.EventOptions.Add(option);
        }

        foreach (var option in InteractionActionOptions)
        {
            row.ActionOptions.Add(option);
        }

        foreach (var option in InteractionTargetOptions)
        {
            row.TargetOptions.Add(option);
        }

        if (!row.TargetOptions.Contains(row.TargetPath))
        {
            row.TargetOptions.Add(row.TargetPath);
        }

        row.PropertyChanged += OnInteractionRuleRowPropertyChanged;
        return row;
    }

    private void OnChartSeriesRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChartSeriesEditorRow.TargetPath) or nameof(ChartSeriesEditorRow.Axis) or nameof(ChartSeriesEditorRow.Style))
        {
            SyncChartSeriesValueFromEntries();
        }
    }

    private void OnInteractionRuleRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ItemInteractionEditorRow.EventName)
            or nameof(ItemInteractionEditorRow.ActionName)
            or nameof(ItemInteractionEditorRow.TargetPath)
            or nameof(ItemInteractionEditorRow.Argument))
        {
            SyncInteractionRuleValueFromEntries();
        }
    }

    private void SyncChartSeriesValueFromEntries()
    {
        var serialized = string.Join(Environment.NewLine, ChartSeriesEntries
            .Where(row => !string.IsNullOrWhiteSpace(row.TargetPath))
            .Select(row => SerializeChartSeriesEntry(row)));

        Parameter.Value = serialized;
        RaisePropertyChanged(nameof(Value));
    }

    private void SyncInteractionRuleValueFromEntries()
    {
        var serialized = ItemInteractionRuleCodec.SerializeDefinitions(InteractionRuleEntries.Select(static row => new ItemInteractionRule
        {
            Event = Enum.TryParse<ItemInteractionEvent>(row.EventName, ignoreCase: true, out var eventKind) ? eventKind : ItemInteractionEvent.BodyLeftClick,
            Action = Enum.TryParse<ItemInteractionAction>(row.ActionName, ignoreCase: true, out var actionKind) ? actionKind : ItemInteractionAction.OpenValueEditor,
            TargetPath = row.TargetPath,
            Argument = row.Argument
        }));

        Parameter.Value = serialized;
        RaisePropertyChanged(nameof(Value));
        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    private static string SerializeChartSeriesEntry(ChartSeriesEditorRow row)
    {
        var axis = NormalizeAxis(row.Axis);
        var style = NormalizeStyle(row.Style);
        return style == "Line"
            ? $"{row.TargetPath}|{axis}"
            : $"{row.TargetPath}|{axis}|{style}";
    }

    private static string NormalizeAxis(string? axis)
    {
        if (string.IsNullOrWhiteSpace(axis))
        {
            return "Y1";
        }

        var trimmed = axis.Trim();
        if (trimmed.StartsWith("Y", true, CultureInfo.InvariantCulture))
        {
            trimmed = trimmed[1..];
        }

        return int.TryParse(trimmed, out var axisIndex)
            ? $"Y{Math.Clamp(axisIndex, 1, 4)}"
            : "Y1";
    }

    private static string NormalizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return "Line";
        }

        return style.Trim().ToLowerInvariant() switch
        {
            "step" => "Step",
            "stephorizontal" => "Step",
            "line" => "Line",
            "straight" => "Line",
            _ => "Line"
        };
    }

    private ChartSeriesEditorRow CloneChartSeriesEntry(ChartSeriesEditorRow source)
    {
        var row = new ChartSeriesEditorRow
        {
            TargetPath = source.TargetPath,
            Axis = NormalizeAxis(source.Axis),
            Style = NormalizeStyle(source.Style)
        };

        foreach (var option in ChartTargetOptions)
        {
            row.TargetOptions.Add(option);
        }

        foreach (var option in ChartAxisOptions)
        {
            row.AxisOptions.Add(option);
        }

        foreach (var option in ChartStyleOptions)
        {
            row.StyleOptions.Add(option);
        }

        return row;
    }

    private ItemInteractionEditorRow CloneInteractionRuleEntry(ItemInteractionEditorRow source)
    {
        var row = new ItemInteractionEditorRow
        {
            EventName = source.EventName,
            ActionName = source.ActionName,
            TargetPath = source.TargetPath,
            Argument = source.Argument
        };

        foreach (var option in InteractionEventOptions)
        {
            row.EventOptions.Add(option);
        }

        foreach (var option in InteractionActionOptions)
        {
            row.ActionOptions.Add(option);
        }

        foreach (var option in InteractionTargetOptions)
        {
            row.TargetOptions.Add(option);
        }

        if (!row.TargetOptions.Contains(row.TargetPath))
        {
            row.TargetOptions.Add(row.TargetPath);
        }

        return row;
    }

    public List<AttachItemEditorRow> CreateAttachItemSnapshot()
        => AttachItemEntries.Select(static row => new AttachItemEditorRow
        {
            RelativePath = row.RelativePath,
            IsAttached = row.IsAttached
        }).ToList();

    public void ApplyAttachItemEntries(IEnumerable<AttachItemEditorRow> rows)
    {
        if (!IsAttachItemList)
        {
            return;
        }

        foreach (var row in AttachItemEntries)
        {
            row.PropertyChanged -= OnAttachItemRowPropertyChanged;
        }

        AttachItemEntries.Clear();
        foreach (var row in rows)
        {
            var copy = new AttachItemEditorRow
            {
                RelativePath = row.RelativePath,
                IsAttached = row.IsAttached
            };

            copy.PropertyChanged += OnAttachItemRowPropertyChanged;
            AttachItemEntries.Add(copy);
        }

        var serialized = string.Join(Environment.NewLine, AttachItemEntries
            .Where(static row => row.IsAttached)
            .Select(static row => row.RelativePath));

        Parameter.Value = serialized;
        RaisePropertyChanged(nameof(Value));
        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    private void RebuildAttachItemEntries()
    {
        foreach (var row in AttachItemEntries)
        {
            row.PropertyChanged -= OnAttachItemRowPropertyChanged;
        }

        AttachItemEntries.Clear();
        var selectedPaths = Value
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var option in Options)
        {
            var row = new AttachItemEditorRow
            {
                RelativePath = option,
                IsAttached = selectedPaths.Contains(option)
            };

            row.PropertyChanged += OnAttachItemRowPropertyChanged;
            AttachItemEntries.Add(row);
        }

        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }

    private void OnAttachItemRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AttachItemEditorRow.IsAttached))
        {
            return;
        }

        var serialized = string.Join(Environment.NewLine, AttachItemEntries
            .Where(static row => row.IsAttached)
            .Select(static row => row.RelativePath));

        Parameter.Value = serialized;
        RaisePropertyChanged(nameof(Value));
        RaisePropertyChanged(nameof(StructuredEditorSummary));
    }
}

