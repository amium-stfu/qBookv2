using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using UiEditor.Host;
using UiEditor.ViewModels;

namespace UiEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Core.UiStateChanged += HandleHostUiStateChanged;
    }

    private void OpenVsCode_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            var codePath = viewModel.BookProjectPath;
            var opened = VsCodeLauncher.OpenFolder(codePath);
            if (opened)
            {
                Core.LogInfo($"VS Code opened for {codePath}");
            }
            else
            {
                Core.LogWarn($"VS Code could not be opened for {codePath}");
            }
        }
        catch (Exception ex)
        {
            Core.LogError("Open VS Code failed", ex);
        }
    }

    private void OpenLogWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.RefreshLog();
        var window = new LogWindow
        {
            DataContext = viewModel
        };
        window.Show();
    }

    private void HandleHostUiStateChanged(string action, BookProject? project)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var methodName = action switch
            {
                "Destroy" => "ApplyDestroyedUi",
                "Run" => "ApplyRunningUi",
                _ => null
            };

            if (string.IsNullOrWhiteSpace(methodName))
            {
                return;
            }

            var method = typeof(MainWindowViewModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(viewModel, [project]);
        });
    }
}
