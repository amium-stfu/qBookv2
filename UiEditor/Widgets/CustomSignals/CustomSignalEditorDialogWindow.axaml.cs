using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class CustomSignalEditorDialogWindow : Window
{
    private readonly MainWindowViewModel? _viewModel;
    private FolderItemModel? _ownerItem;
    private IReadOnlyList<string> _sourceOptions = Array.Empty<string>();
    private CustomSignalDefinition? _result;

    public CustomSignalEditorDialogWindow()
    {
        ViewModel = new CustomSignalEditorDialogViewModel(null, new FolderItemModel(), null);
        DataContext = ViewModel;
        InitializeComponent();
    }

    public CustomSignalEditorDialogWindow(MainWindowViewModel? viewModel, FolderItemModel ownerItem, CustomSignalDefinition? definition, IEnumerable<string> sourceOptions)
        : this()
    {
        _viewModel = viewModel;
        _ownerItem = ownerItem;
        _sourceOptions = sourceOptions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        ViewModel = new CustomSignalEditorDialogViewModel(viewModel, ownerItem, definition);
        DataContext = ViewModel;
    }

    public CustomSignalEditorDialogViewModel ViewModel { get; }

    public static async Task<CustomSignalDefinition?> ShowAsync(Window owner, MainWindowViewModel? viewModel, FolderItemModel ownerItem, CustomSignalDefinition? definition, IEnumerable<string> sourceOptions)
    {
        var dialog = new CustomSignalEditorDialogWindow(viewModel, ownerItem, definition, sourceOptions);
        return await dialog.ShowDialog<CustomSignalDefinition?>(owner);
    }

    private async void OnPickSourceAClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.SourcePath = await PickTargetAsync(ViewModel.SourcePath) ?? ViewModel.SourcePath;
        e.Handled = true;
    }

    private async void OnPickSourceBClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.SourcePath2 = await PickTargetAsync(ViewModel.SourcePath2) ?? ViewModel.SourcePath2;
        e.Handled = true;
    }

    private async void OnPickSourceCClicked(object? sender, RoutedEventArgs e)
    {
        ViewModel.SourcePath3 = await PickTargetAsync(ViewModel.SourcePath3) ?? ViewModel.SourcePath3;
        e.Handled = true;
    }

    private async Task<string?> PickTargetAsync(string currentSelection)
    {
        var dialog = new TargetTreeSelectionDialogWindow(_viewModel, _sourceOptions, currentSelection, _ownerItem?.FolderName ?? string.Empty);
        await dialog.ShowDialog(this);
        return string.IsNullOrWhiteSpace(dialog.CommittedSelection) ? currentSelection : dialog.CommittedSelection;
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryBuildDefinition(out var definition, out var errorMessage))
        {
            ViewModel.ErrorMessage = errorMessage;
            return;
        }

        _result = definition;
        Close(_result);
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close((CustomSignalDefinition?)null);
        e.Handled = true;
    }
}

public sealed class CustomSignalEditorDialogViewModel : ObservableObject
{
    private readonly FolderItemModel _ownerItem;
    private string _name = string.Empty;
    private string _selectedMode = CustomSignalMode.Input.ToString();
    private string _selectedDataType = CustomSignalDataType.Number.ToString();
    private bool _isWritable = true;
    private string _unit = string.Empty;
    private string _format = string.Empty;
    private string _valueText = string.Empty;
    private string _selectedOperation = CustomSignalOperation.Copy.ToString();
    private string _sourcePath = string.Empty;
    private string _sourcePath2 = string.Empty;
    private string _sourcePath3 = string.Empty;
    private string _errorMessage = string.Empty;

    public CustomSignalEditorDialogViewModel(MainWindowViewModel? mainWindowViewModel, FolderItemModel ownerItem, CustomSignalDefinition? definition)
    {
        _ownerItem = ownerItem;
        ModeOptions = Enum.GetNames<CustomSignalMode>();
        DataTypeOptions = Enum.GetNames<CustomSignalDataType>();
        OperationOptions = Enum.GetNames<CustomSignalOperation>();

        DialogBackground = mainWindowViewModel?.DialogBackground ?? "#E3E5EE";
        SectionBackground = mainWindowViewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
        BorderColor = mainWindowViewModel?.CardBorderBrush ?? "#D5D9E0";
        PrimaryTextBrush = mainWindowViewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = mainWindowViewModel?.SecondaryTextBrush ?? "#5E6777";
        EditorBackground = mainWindowViewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        EditorForeground = mainWindowViewModel?.ParameterEditForeColor ?? "#111827";
        ButtonBackground = mainWindowViewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = mainWindowViewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = mainWindowViewModel?.PrimaryTextBrush ?? "#111827";

        if (definition is null)
        {
            return;
        }

        Name = definition.Name;
        SelectedMode = definition.Mode.ToString();
        SelectedDataType = definition.DataType.ToString();
        IsWritable = definition.IsWritable;
        Unit = definition.Unit;
        Format = definition.Format;
        ValueText = definition.ValueText;
        SelectedOperation = definition.Operation.ToString();
        SourcePath = definition.SourcePath;
        SourcePath2 = definition.SourcePath2;
        SourcePath3 = definition.SourcePath3;
    }

    public IReadOnlyList<string> ModeOptions { get; }

    public IReadOnlyList<string> DataTypeOptions { get; }

    public IReadOnlyList<string> OperationOptions { get; }

    public string DialogBackground { get; }

    public string SectionBackground { get; }

    public string BorderColor { get; }

    public string PrimaryTextBrush { get; }

    public string SecondaryTextBrush { get; }

    public string EditorBackground { get; }

    public string EditorForeground { get; }

    public string ButtonBackground { get; }

    public string ButtonBorderBrush { get; }

    public string ButtonForeground { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                RaisePropertyChanged(nameof(PreviewPath));
            }
        }
    }

    public string SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value ?? CustomSignalMode.Input.ToString()))
            {
                RaiseComputedVisibilityChanged();
                RaisePropertyChanged(nameof(PreviewPath));
            }
        }
    }

    public string SelectedDataType
    {
        get => _selectedDataType;
        set => SetProperty(ref _selectedDataType, value ?? CustomSignalDataType.Number.ToString());
    }

    public bool IsWritable
    {
        get => _isWritable;
        set => SetProperty(ref _isWritable, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value ?? string.Empty);
    }

    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value ?? string.Empty);
    }

    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value ?? string.Empty);
    }

    public string SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (SetProperty(ref _selectedOperation, value ?? CustomSignalOperation.Copy.ToString()))
            {
                RaiseComputedVisibilityChanged();
            }
        }
    }

    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value ?? string.Empty);
    }

    public string SourcePath2
    {
        get => _sourcePath2;
        set => SetProperty(ref _sourcePath2, value ?? string.Empty);
    }

    public string SourcePath3
    {
        get => _sourcePath3;
        set => SetProperty(ref _sourcePath3, value ?? string.Empty);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value ?? string.Empty);
    }

    public bool IsComputed => string.Equals(SelectedMode, CustomSignalMode.Computed.ToString(), StringComparison.OrdinalIgnoreCase);

    public bool IsValueVisible => !IsComputed;

    public bool ShowSourceA => IsComputed;

    public bool ShowSourceB => IsComputed && SelectedOperation is not nameof(CustomSignalOperation.Copy);

    public bool ShowSourceC => IsComputed && SelectedOperation == nameof(CustomSignalOperation.If);

    public string PreviewPath
    {
        get
        {
            var definition = new CustomSignalDefinition { Name = Name };
            return CustomSignalsControl.BuildRegistryPath(_ownerItem, definition);
        }
    }

    public bool TryBuildDefinition(out CustomSignalDefinition? definition, out string errorMessage)
    {
        definition = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            errorMessage = "Name is required.";
            return false;
        }

        if (!Enum.TryParse<CustomSignalMode>(SelectedMode, true, out var mode))
        {
            errorMessage = "Mode is invalid.";
            return false;
        }

        if (!Enum.TryParse<CustomSignalDataType>(SelectedDataType, true, out var dataType))
        {
            errorMessage = "Type is invalid.";
            return false;
        }

        if (!Enum.TryParse<CustomSignalOperation>(SelectedOperation, true, out var operation))
        {
            operation = CustomSignalOperation.Copy;
        }

        if (mode == CustomSignalMode.Computed && string.IsNullOrWhiteSpace(SourcePath))
        {
            errorMessage = "Source A is required for computed signals.";
            return false;
        }

        definition = new CustomSignalDefinition
        {
            Name = Name.Trim(),
            Mode = mode,
            DataType = dataType,
            IsWritable = mode == CustomSignalMode.Input && IsWritable,
            Unit = Unit.Trim(),
            Format = Format.Trim(),
            ValueText = ValueText,
            Operation = operation,
            SourcePath = SourcePath.Trim(),
            SourcePath2 = SourcePath2.Trim(),
            SourcePath3 = SourcePath3.Trim()
        };

        return true;
    }

    private void RaiseComputedVisibilityChanged()
    {
        RaisePropertyChanged(nameof(IsComputed));
        RaisePropertyChanged(nameof(IsValueVisible));
        RaisePropertyChanged(nameof(ShowSourceA));
        RaisePropertyChanged(nameof(ShowSourceB));
        RaisePropertyChanged(nameof(ShowSourceC));
    }
}