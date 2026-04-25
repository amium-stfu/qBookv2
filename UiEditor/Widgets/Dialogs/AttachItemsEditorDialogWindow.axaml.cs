using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Amium.Host;
using Amium.UiEditor.Helpers;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class AttachItemsEditorDialogWindow : Window, INotifyPropertyChanged
{
    private readonly EditorDialogField? _field;
    private MainWindowViewModel? _viewModel;
    private string _dialogBackground = "#E3E5EE";
    private string _borderColor = "#D5D9E0";
    private string _primaryTextBrush = "#111827";
    private string _secondaryTextBrush = "#5E6777";
    private string _buttonBackground = "#F8FAFC";
    private string _buttonBorderBrush = "#CBD5E1";
    private string _buttonForeground = "#111827";
    private string _editorDialogSectionContentBackground = "#EEF3F8";
    private bool _showIntervalColumn;
    private bool _isOpeningDemoModules;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public AttachItemsEditorDialogWindow()
    {
        Rows = [];
        InitializeComponent();
        DataContext = this;
    }

    public AttachItemsEditorDialogWindow(MainWindowViewModel? viewModel, EditorDialogField field)
    {
        _field = field;
        Rows = new ObservableCollection<AttachItemEditorRow>(field.CreateAttachItemSnapshot());
        InitializeComponent();
        DataContext = this;
        AttachToViewModel(viewModel);

        foreach (var row in Rows)
        {
            row.PropertyChanged += OnRowPropertyChanged;
        }

        ShowIntervalColumn = field.OwnerItem?.IsSqlLoggerControl == true
                             && string.Equals(field.Key, "CsvSignalPaths", StringComparison.Ordinal);

        if (ShowIntervalColumn)
        {
            Width = 640;
            MinWidth = 600;
        }
    }

    public ObservableCollection<AttachItemEditorRow> Rows { get; }

    public bool ShowIntervalColumn
    {
        get => _showIntervalColumn;
        private set => SetAndRaise(ref _showIntervalColumn, value, nameof(ShowIntervalColumn));
    }

    public bool ShowAddDemoModuleButton
        => _field?.OwnerItem is { IsUdlClientControl: true, UdlClientDemoEnabled: true }
           && string.Equals(_field.Key, "UdlAttachedItemPaths", StringComparison.Ordinal);

    public string ToggleSelectionButtonText
        => Rows.Count > 0 && Rows.All(static row => row.IsAttached)
            ? "Unselect All"
            : "Select All";

    public bool CanOpenDemoModules
    {
        get => !_isOpeningDemoModules;
        private set => SetAndRaise(ref _isOpeningDemoModules, !value, nameof(CanOpenDemoModules));
    }

    public string DialogBackground
    {
        get => _dialogBackground;
        private set => SetAndRaise(ref _dialogBackground, value, nameof(DialogBackground));
    }

    public string BorderColor
    {
        get => _borderColor;
        private set => SetAndRaise(ref _borderColor, value, nameof(BorderColor));
    }

    public string PrimaryTextBrush
    {
        get => _primaryTextBrush;
        private set => SetAndRaise(ref _primaryTextBrush, value, nameof(PrimaryTextBrush));
    }

    public string SecondaryTextBrush
    {
        get => _secondaryTextBrush;
        private set => SetAndRaise(ref _secondaryTextBrush, value, nameof(SecondaryTextBrush));
    }

    public string ButtonBackground
    {
        get => _buttonBackground;
        private set => SetAndRaise(ref _buttonBackground, value, nameof(ButtonBackground));
    }

    public string ButtonBorderBrush
    {
        get => _buttonBorderBrush;
        private set => SetAndRaise(ref _buttonBorderBrush, value, nameof(ButtonBorderBrush));
    }

    public string ButtonForeground
    {
        get => _buttonForeground;
        private set => SetAndRaise(ref _buttonForeground, value, nameof(ButtonForeground));
    }

    public string EditorDialogSectionContentBackground
    {
        get => _editorDialogSectionContentBackground;
        private set => SetAndRaise(ref _editorDialogSectionContentBackground, value, nameof(EditorDialogSectionContentBackground));
    }

    protected override void OnClosed(System.EventArgs e)
    {
        AttachToViewModel(null);
        base.OnClosed(e);
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        _field?.ApplyAttachItemEntries(Rows);
        Close();
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
        e.Handled = true;
    }

    private async void OnAddDemoModuleClicked(object? sender, RoutedEventArgs e)
    {
        if (_field?.OwnerItem is not { IsUdlClientControl: true, UdlClientDemoEnabled: true } ownerItem
            || TopLevel.GetTopLevel(this) is not Window owner
            || !CanOpenDemoModules)
        {
            e.Handled = true;
            return;
        }

        CanOpenDemoModules = false;
        try
        {
            var previousModuleNames = GetDemoModuleNames(ownerItem.UdlDemoModuleDefinitions);
            var updatedDefinitions = await UdlDemoModulesDialogWindow.ShowAsync(owner, _viewModel, ownerItem, ownerItem.UdlDemoModuleDefinitions);
            if (updatedDefinitions is not null && !string.Equals(updatedDefinitions, ownerItem.UdlDemoModuleDefinitions, StringComparison.Ordinal))
            {
                ownerItem.UdlDemoModuleDefinitions = updatedDefinitions;
            }

            RefreshRowsFromField();

            if (updatedDefinitions is not null)
            {
                SelectNewDemoModules(previousModuleNames, GetDemoModuleNames(ownerItem.UdlDemoModuleDefinitions));
            }
        }
        finally
        {
            CanOpenDemoModules = true;
        }

        e.Handled = true;
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateThemeBindings();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.DialogBackground)
            || e.PropertyName == nameof(MainWindowViewModel.CardBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.PrimaryTextBrush)
            || e.PropertyName == nameof(MainWindowViewModel.SecondaryTextBrush)
            || e.PropertyName == nameof(MainWindowViewModel.EditPanelButtonBackground)
            || e.PropertyName == nameof(MainWindowViewModel.EditPanelButtonBorderBrush)
            || e.PropertyName == nameof(MainWindowViewModel.EditorDialogSectionContentBackground))
        {
            UpdateThemeBindings();
        }
    }

    private void UpdateThemeBindings()
    {
        DialogBackground = _viewModel?.DialogBackground ?? "#E3E5EE";
        BorderColor = _viewModel?.CardBorderBrush ?? "#D5D9E0";
        PrimaryTextBrush = _viewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = _viewModel?.SecondaryTextBrush ?? "#5E6777";
        ButtonBackground = _viewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = _viewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = _viewModel?.PrimaryTextBrush ?? "#111827";
        EditorDialogSectionContentBackground = _viewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
    }

    private void RefreshRowsFromField()
    {
        if (_field is null)
        {
            return;
        }

        if (_field.OwnerItem is { IsUdlClientControl: true } ownerItem)
        {
            _field.RefreshAttachItemOptions(GetUdlAttachItemOptions(ownerItem));
        }
        else
        {
            _field.InitializeAttachItemEditor();
        }

        foreach (var existingRow in Rows)
        {
            existingRow.PropertyChanged -= OnRowPropertyChanged;
        }

        Rows.Clear();
        foreach (var row in _field.CreateAttachItemSnapshot())
        {
            row.PropertyChanged += OnRowPropertyChanged;
            Rows.Add(row);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowAddDemoModuleButton)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleSelectionButtonText)));
    }

    private void OnToggleSelectionClicked(object? sender, RoutedEventArgs e)
    {
        var selectAll = Rows.Any(static row => !row.IsAttached);
        foreach (var row in Rows)
        {
            row.IsAttached = selectAll;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleSelectionButtonText)));
        e.Handled = true;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AttachItemEditorRow.IsAttached))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleSelectionButtonText)));
        }
    }

    private void SelectNewDemoModules(ISet<string> previousModuleNames, ISet<string> currentModuleNames)
    {
        if (currentModuleNames.Count == 0)
        {
            return;
        }

        var addedModuleNames = currentModuleNames
            .Where(name => !previousModuleNames.Contains(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (addedModuleNames.Count == 0)
        {
            return;
        }

        foreach (var row in Rows.Where(row => addedModuleNames.Contains(row.RelativePath)))
        {
            row.IsAttached = true;
        }
    }

    private static HashSet<string> GetDemoModuleNames(string? rawDefinitions)
    {
        return UdlDemoModuleDefinitionCodec.ParseDefinitions(rawDefinitions)
            .Select(static definition => definition.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetUdlAttachItemOptions(FolderItemModel item)
    {
        var normalizedName = NormalizeUdlClientName(item.Name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return [];
        }

        var prefixes = new[]
        {
            $"Project.{item.FolderName}.{normalizedName}.Status.AttachOptions",
            $"UdlProject.{item.FolderName}.{normalizedName}.Status.AttachOptions",
            $"Runtime.UdlClient.{normalizedName}"
        };

        return HostRegistries.Data.GetAllKeys()
            .SelectMany(key => prefixes.Select(prefix => TryGetUdlAttachRootOption(key, prefix, TargetPathHelper.NormalizeComparablePath(prefix))))
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeUdlClientName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        var normalized = rawName.Trim();
        if (normalized.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^"Client".Length];
        }

        return normalized.Trim();
    }

    private static string? TryGetUdlAttachRootOption(string key, string attachOptionsPrefix, string comparablePrefix)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalizedKey = TargetPathHelper.NormalizeComparablePath(key);
        if (!normalizedKey.StartsWith(comparablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var remainder = key.Length > attachOptionsPrefix.Length
            ? key[attachOptionsPrefix.Length..]
            : string.Empty;
        remainder = remainder.TrimStart('.', '/');
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return null;
        }

        var separatorIndex = remainder.IndexOfAny(['.', '/']);
        return separatorIndex > 0
            ? remainder[..separatorIndex]
            : remainder;
    }

    private void SetAndRaise(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetAndRaise(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
