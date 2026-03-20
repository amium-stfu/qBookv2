using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using UiEditor.Host;
using UiEditor.Items;

namespace UiEditor.ViewModels;

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
    }

    public EditorDialogBindingDefinition Definition { get; }

    public Parameter Parameter { get; }

    public string Key => Definition.Key;

    public string Label => Definition.Label;

    public EditorPropertyType PropertyType => Definition.PropertyType;

    public bool IsReadOnly => Definition.IsReadOnly;

    public ObservableCollection<string> Options { get; } = [];

    public ObservableCollection<ChartSeriesEditorRow> ChartSeriesEntries { get; } = [];

    public ObservableCollection<string> ChartTargetOptions { get; } = [];

    public ObservableCollection<string> ChartAxisOptions { get; } = [];

    private string _toolTipText = string.Empty;
    private string _newChartTargetPath = string.Empty;
    private string _newChartAxis = "Y1";

    public string ToolTipText
    {
        get => _toolTipText;
        set => SetProperty(ref _toolTipText, value);
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

            if (IsChartSeriesList)
            {
                RebuildChartSeriesEntries();
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

    public bool IsChoice => PropertyType == EditorPropertyType.Choice;

    public bool IsColor => PropertyType == EditorPropertyType.Color;

    public bool IsMultilineText => PropertyType == EditorPropertyType.MultilineText;

    public bool IsChartSeriesList => PropertyType == EditorPropertyType.ChartSeriesList;

    public bool IsTextInput => !IsChoice && !IsReadOnly && !IsMultilineText && !IsChartSeriesList;

    public bool ShowPickerButton => IsColor && !IsReadOnly;

    public string PreviewColor => string.IsNullOrWhiteSpace(Value) ? "Transparent" : Value;

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
    }

    public void AddChartSeriesEntry()
    {
        if (!IsChartSeriesList || string.IsNullOrWhiteSpace(NewChartTargetPath))
        {
            return;
        }

        var row = CreateChartSeriesEntry(NewChartTargetPath, NewChartAxis);
        ChartSeriesEntries.Add(row);
        SyncChartSeriesValueFromEntries();
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
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                continue;
            }

            ChartSeriesEntries.Add(CreateChartSeriesEntry(targetPath, axis));
        }
    }

    private ChartSeriesEditorRow CreateChartSeriesEntry(string targetPath, string axis)
    {
        var row = new ChartSeriesEditorRow
        {
            TargetPath = targetPath,
            Axis = NormalizeAxis(axis)
        };

        foreach (var option in ChartTargetOptions)
        {
            row.TargetOptions.Add(option);
        }

        foreach (var option in ChartAxisOptions)
        {
            row.AxisOptions.Add(option);
        }

        row.PropertyChanged += OnChartSeriesRowPropertyChanged;
        return row;
    }

    private void OnChartSeriesRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChartSeriesEditorRow.TargetPath) or nameof(ChartSeriesEditorRow.Axis))
        {
            SyncChartSeriesValueFromEntries();
        }
    }

    private void SyncChartSeriesValueFromEntries()
    {
        var serialized = string.Join(Environment.NewLine, ChartSeriesEntries
            .Where(row => !string.IsNullOrWhiteSpace(row.TargetPath))
            .Select(row => $"{row.TargetPath}|{NormalizeAxis(row.Axis)}"));

        Parameter.Value = serialized;
        RaisePropertyChanged(nameof(Value));
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
}

