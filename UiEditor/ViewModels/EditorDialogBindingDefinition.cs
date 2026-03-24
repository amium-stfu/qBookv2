using System;
using System.Collections.Generic;
using Amium.Items;
using Amium.UiEditor.Models;

namespace Amium.UiEditor.ViewModels;

public sealed class EditorDialogBindingDefinition
{
    public EditorDialogBindingDefinition(
        string key,
        string label,
        EditorPropertyType propertyType,
        Func<PageItemModel, string> readValue,
        Func<PageItemModel, string, string?>? applyValue = null,
        bool isReadOnly = false,
        Func<PageItemModel, IEnumerable<string>>? optionsFactory = null,
        Func<PageItemModel, string>? toolTipFactory = null)
    {
        Key = key;
        Label = label;
        PropertyType = propertyType;
        ReadValue = readValue;
        ApplyValue = applyValue;
        IsReadOnly = isReadOnly;
        OptionsFactory = optionsFactory;
        ToolTipFactory = toolTipFactory;
    }

    public string Key { get; }

    public string Label { get; }

    public EditorPropertyType PropertyType { get; }

    public bool IsReadOnly { get; }

    public Func<PageItemModel, string> ReadValue { get; }

    public Func<PageItemModel, string, string?>? ApplyValue { get; }

    public Func<PageItemModel, IEnumerable<string>>? OptionsFactory { get; }

    public Func<PageItemModel, string>? ToolTipFactory { get; }

    public EditorDialogField CreateField(PageItemModel item)
    {
        var parameterPath = string.IsNullOrWhiteSpace(item.Path) ? Key : $"{item.Path}.{Key}";
        var field = new EditorDialogField(this, new Parameter(Key, ReadValue(item), parameterPath));
        if (OptionsFactory is not null)
        {
            foreach (var option in OptionsFactory(item))
            {
                field.Options.Add(option);
            }
        }

        field.ToolTipText = ToolTipFactory?.Invoke(item) ?? string.Empty;
        field.InitializeChartSeriesEditor();
        field.InitializeAttachItemEditor();
        return field;
    }

    public string? Apply(PageItemModel item, string value)
        => ApplyValue?.Invoke(item, value);
}
