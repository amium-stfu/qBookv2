using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public sealed partial class PythonEnvErrorDialogWindow : Window
{
    public PythonEnvErrorDialogWindow()
    {
        InitializeComponent();
        KeyDown += OnWindowKeyDown;
    }

    public void Initialize(MainWindowViewModel? viewModel, PythonEnvErrorDetails details)
    {
        DataContext = new PythonEnvErrorDialogViewModel(viewModel, details);
        Dispatcher.UIThread.Post(() => CloseButton.Focus(), DispatcherPriority.Input);
    }

    private void OnCloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}

public sealed class PythonEnvErrorDialogViewModel : ObservableObject
{
    public PythonEnvErrorDialogViewModel(MainWindowViewModel? viewModel, PythonEnvErrorDetails details)
    {
        DialogBackground = viewModel?.DialogBackground ?? "#E3E5EE";
        CardBorderBrush = viewModel?.CardBorderBrush ?? "#D5D9E0";
        ParameterEditBackgrundColor = viewModel?.ParameterEditBackgrundColor ?? "#FFFFFF";
        ParameterEditForeColor = viewModel?.ParameterEditForeColor ?? "#111827";
        EditPanelButtonBackground = viewModel?.EditPanelButtonBackground ?? "#F8FAFC";
        EditPanelButtonBorderBrush = viewModel?.EditPanelButtonBorderBrush ?? "#CBD5E1";
        PrimaryTextBrush = viewModel?.PrimaryTextBrush ?? "#111827";
        SecondaryTextBrush = viewModel?.SecondaryTextBrush ?? "#5E6777";
        SummaryText = details.Summary;
        EnvironmentName = details.EnvironmentName;
        FileText = string.IsNullOrWhiteSpace(details.File) ? "n/a" : details.File!;
        LineText = details.LineNumber is int lineNumber ? lineNumber.ToString() : "n/a";
        FunctionText = string.IsNullOrWhiteSpace(details.FunctionName) ? "n/a" : details.FunctionName!;
        DetailsText = string.IsNullOrWhiteSpace(details.Traceback)
            ? (details.FullMessage ?? details.Summary)
            : details.Traceback!;
    }

    public string DialogBackground { get; }
    public string CardBorderBrush { get; }
    public string ParameterEditBackgrundColor { get; }
    public string ParameterEditForeColor { get; }
    public string EditPanelButtonBackground { get; }
    public string EditPanelButtonBorderBrush { get; }
    public string PrimaryTextBrush { get; }
    public string SecondaryTextBrush { get; }
    public string SummaryText { get; }
    public string EnvironmentName { get; }
    public string FileText { get; }
    public string LineText { get; }
    public string FunctionText { get; }
    public string DetailsText { get; }
}