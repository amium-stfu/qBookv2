using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HornetStudio.Editor.Models;
using HornetStudio.Editor.ViewModels;

namespace HornetStudio.Editor.Widgets;

public sealed class SetValueOperationOption
{
    public SetValueOperationKind Kind { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}

public partial class SetValueOperationEditorDialogWindow : Window, INotifyPropertyChanged
{
    private MainWindowViewModel? _viewModel;
    private string _dialogBackground = "#E3E5EE";
    private string _borderColor = "#D5D9E0";
    private string _primaryTextBrush = "#111827";
    private string _secondaryTextBrush = "#5E6777";
    private string _buttonBackground = "#F8FAFC";
    private string _buttonBorderBrush = "#CBD5E1";
    private string _buttonForeground = "#111827";
    private string _inputBackground = "#FFFFFF";
    private string _inputForeground = "#111827";
    private string _editorDialogSectionContentBackground = "#EEF3F8";
    private SetValueOperationOption? _selectedOperationOption;
    private string _literalValue = string.Empty;
    private string _separatorValue = string.Empty;
    private string _selectedSourcePath = string.Empty;
    private string _validationMessage = string.Empty;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public SetValueOperationEditorDialogWindow()
    {
        OperationOptions = [];
        SourceOptions = [];
        InitializeComponent();
        DataContext = this;
    }

    public SetValueOperationEditorDialogWindow(
        MainWindowViewModel? viewModel,
        string targetPath,
        SetValueTargetKind targetKind,
        string rawArgument,
        IEnumerable<string> sourceOptions)
        : this()
    {
        _viewModel = viewModel;
        TargetPath = string.IsNullOrWhiteSpace(targetPath) ? "this" : targetPath;
        TargetKind = targetKind;

        foreach (var option in BuildOptions(targetKind))
        {
            OperationOptions.Add(option);
        }

        foreach (var option in sourceOptions.Where(static option => !string.IsNullOrWhiteSpace(option)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            SourceOptions.Add(option);
        }

        var parsed = SetValueOperationCodec.Parse(rawArgument);
        var operation = parsed.IsValid ? parsed.Operation : new SetValueOperation { Kind = SetValueOperationKind.SetLiteral, LiteralValue = rawArgument ?? string.Empty, IsLegacyLiteral = true };
        var selectedKind = operation.IsLegacyLiteral ? SetValueOperationKind.SetLiteral : operation.Kind;
        SelectedOperationOption = OperationOptions.FirstOrDefault(option => option.Kind == selectedKind) ?? OperationOptions.FirstOrDefault();
        LiteralValue = operation.LiteralValue;
        SeparatorValue = operation.Separator;
        SelectedSourcePath = operation.SourcePath;
        if (!string.IsNullOrWhiteSpace(SelectedSourcePath) && !SourceOptions.Contains(SelectedSourcePath))
        {
            SourceOptions.Add(SelectedSourcePath);
        }

        UpdateThemeBindings();
        RefreshValidation();
    }

    public ObservableCollection<SetValueOperationOption> OperationOptions { get; }

    public ObservableCollection<string> SourceOptions { get; }

    public string TargetPath { get; } = "this";

    public SetValueTargetKind TargetKind { get; }

    public string TargetKindText => TargetKind switch
    {
        SetValueTargetKind.Numeric => "Numeric",
        SetValueTargetKind.String => "String",
        SetValueTargetKind.Boolean => "Boolean",
        _ => "Unknown"
    };

    public string CurrentSummary => PreviewSummary;

    public string PreviewSummary => SelectedOperationOption is null
        ? "No operation selected"
        : SetValueOperationCodec.GetSummary(BuildOperation(), TargetKind);

    public string LiteralWatermark => TargetKind switch
    {
        SetValueTargetKind.Numeric => "12.5",
        SetValueTargetKind.Boolean => "true",
        _ => "Value"
    };

    public bool ShowsLiteralValue => SelectedOperationOption is not null && UsesLiteralValue(SelectedOperationOption.Kind);

    public bool ShowsSeparatorValue => SelectedOperationOption?.Kind == SetValueOperationKind.AppendText && TargetKind == SetValueTargetKind.String;

    public bool ShowsSourcePicker => SelectedOperationOption?.Kind == SetValueOperationKind.SetFromItem;

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanSave => SelectedOperationOption is not null && string.IsNullOrWhiteSpace(ValidationMessage);

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (_validationMessage == value)
            {
                return;
            }

            _validationMessage = value;
            RaisePropertyChanged(nameof(ValidationMessage));
            RaisePropertyChanged(nameof(HasValidationError));
            RaisePropertyChanged(nameof(CanSave));
        }
    }

    public string SerializedArgument { get; private set; } = string.Empty;

    public SetValueOperationOption? SelectedOperationOption
    {
        get => _selectedOperationOption;
        set
        {
            if (ReferenceEquals(_selectedOperationOption, value))
            {
                return;
            }

            _selectedOperationOption = value;
            RaisePropertyChanged(nameof(SelectedOperationOption));
            RaisePropertyChanged(nameof(ShowsLiteralValue));
            RaisePropertyChanged(nameof(ShowsSeparatorValue));
            RaisePropertyChanged(nameof(ShowsSourcePicker));
            RaisePropertyChanged(nameof(PreviewSummary));
            RaisePropertyChanged(nameof(CurrentSummary));
            RefreshValidation();
        }
    }

    public string LiteralValue
    {
        get => _literalValue;
        set
        {
            var normalized = value ?? string.Empty;
            if (_literalValue == normalized)
            {
                return;
            }

            _literalValue = normalized;
            RaisePropertyChanged(nameof(LiteralValue));
            RaisePropertyChanged(nameof(PreviewSummary));
            RaisePropertyChanged(nameof(CurrentSummary));
            RefreshValidation();
        }
    }

    public string SeparatorValue
    {
        get => _separatorValue;
        set
        {
            var normalized = value ?? string.Empty;
            if (_separatorValue == normalized)
            {
                return;
            }

            _separatorValue = normalized;
            RaisePropertyChanged(nameof(SeparatorValue));
            RaisePropertyChanged(nameof(PreviewSummary));
            RaisePropertyChanged(nameof(CurrentSummary));
            RefreshValidation();
        }
    }

    public string SelectedSourcePath
    {
        get => _selectedSourcePath;
        set
        {
            var normalized = value ?? string.Empty;
            if (_selectedSourcePath == normalized)
            {
                return;
            }

            _selectedSourcePath = normalized;
            RaisePropertyChanged(nameof(SelectedSourcePath));
            RaisePropertyChanged(nameof(PreviewSummary));
            RaisePropertyChanged(nameof(CurrentSummary));
            RefreshValidation();
        }
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

    public string InputBackground
    {
        get => _inputBackground;
        private set => SetAndRaise(ref _inputBackground, value, nameof(InputBackground));
    }

    public string InputForeground
    {
        get => _inputForeground;
        private set => SetAndRaise(ref _inputForeground, value, nameof(InputForeground));
    }

    public string EditorDialogSectionContentBackground
    {
        get => _editorDialogSectionContentBackground;
        private set => SetAndRaise(ref _editorDialogSectionContentBackground, value, nameof(EditorDialogSectionContentBackground));
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var operation = BuildOperation();
        SerializedArgument = SetValueOperationCodec.Serialize(operation);
        Close(SerializedArgument);
        e.Handled = true;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
        e.Handled = true;
    }

    private SetValueOperation BuildOperation()
        => new()
        {
            Kind = SelectedOperationOption?.Kind ?? SetValueOperationKind.SetLiteral,
            LiteralValue = LiteralValue,
            Separator = SelectedOperationOption?.Kind == SetValueOperationKind.AppendText ? SeparatorValue : string.Empty,
            SourcePath = SelectedSourcePath,
            IsLegacyLiteral = false
        };

    private void RefreshValidation()
    {
        if (SelectedOperationOption is null)
        {
            ValidationMessage = "Select a SetValue operation.";
            return;
        }

        var validation = SetValueOperationCodec.Validate(
            operation: BuildOperation(),
            targetKind: TargetKind,
            isCompatibleSourcePath: sourcePath => SourceOptions.Contains(sourcePath, StringComparer.OrdinalIgnoreCase));
        ValidationMessage = validation.IsValid ? string.Empty : validation.ErrorMessage;
    }

    private static IReadOnlyList<SetValueOperationOption> BuildOptions(SetValueTargetKind targetKind)
        => targetKind switch
        {
            SetValueTargetKind.Numeric =>
            [
                new SetValueOperationOption { Kind = SetValueOperationKind.SetLiteral, DisplayName = "Set literal value" },
                new SetValueOperationOption { Kind = SetValueOperationKind.IncrementBy, DisplayName = "Increase by" },
                new SetValueOperationOption { Kind = SetValueOperationKind.DecrementBy, DisplayName = "Decrease by" },
                new SetValueOperationOption { Kind = SetValueOperationKind.IncrementOne, DisplayName = "Increase by 1" },
                new SetValueOperationOption { Kind = SetValueOperationKind.DecrementOne, DisplayName = "Decrease by 1" },
                new SetValueOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayName = "Set from item" }
            ],
            SetValueTargetKind.String =>
            [
                new SetValueOperationOption { Kind = SetValueOperationKind.SetLiteral, DisplayName = "Set literal text" },
                new SetValueOperationOption { Kind = SetValueOperationKind.AppendText, DisplayName = "Append text" },
                new SetValueOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayName = "Set from item" }
            ],
            SetValueTargetKind.Boolean =>
            [
                new SetValueOperationOption { Kind = SetValueOperationKind.SetTrue, DisplayName = "Set true" },
                new SetValueOperationOption { Kind = SetValueOperationKind.SetFalse, DisplayName = "Set false" },
                new SetValueOperationOption { Kind = SetValueOperationKind.SetLiteral, DisplayName = "Set literal value" },
                new SetValueOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayName = "Set from item" }
            ],
            _ =>
            [
                new SetValueOperationOption { Kind = SetValueOperationKind.SetLiteral, DisplayName = "Set literal value" },
                new SetValueOperationOption { Kind = SetValueOperationKind.SetFromItem, DisplayName = "Set from item" }
            ]
        };

    private static bool UsesLiteralValue(SetValueOperationKind kind)
        => kind is SetValueOperationKind.SetLiteral
            or SetValueOperationKind.IncrementBy
            or SetValueOperationKind.DecrementBy
            or SetValueOperationKind.AppendText;

    private void UpdateThemeBindings()
    {
        DialogBackground = _viewModel?.DialogBackground ?? "#E3E5EE";
        BorderColor = _viewModel?.CardBorderBrush ?? "#D5D9E0";
        PrimaryTextBrush = _viewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = _viewModel?.SecondaryTextBrush ?? "#5E6777";
        ButtonBackground = _viewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        ButtonBorderBrush = _viewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        ButtonForeground = _viewModel?.PrimaryTextBrush ?? "#111827";
        InputBackground = _viewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        InputForeground = _viewModel?.ParameterEditForeColor ?? "#111827";
        EditorDialogSectionContentBackground = _viewModel?.EditorDialogSectionContentBackground ?? "#EEF3F8";
    }

    private void SetAndRaise(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        RaisePropertyChanged(propertyName);
    }

    private void RaisePropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
