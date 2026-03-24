using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Amium.Host;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor;

public partial class MainWindow : Window
{
    private LogWindow? _logWindow;

    public MainWindow()
    {
        InitializeComponent();
        Core.UiStateChanged += HandleHostUiStateChanged;
    }

    private async void LoadBook_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AmiumStudioMainWindowViewModel viewModel)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Book-Ordner wählen",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        viewModel.BookProjectPath = folders[0].Path.LocalPath;

        if (viewModel.LoadBookCommand.CanExecute(null))
        {
            viewModel.LoadBookCommand.Execute(null);
        }
    }

    private void OpenVsCode_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AmiumStudioMainWindowViewModel viewModel)
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

    private void OnTabPlacementTopClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.SetTabStripPlacementCommand.CanExecute("Top"))
        {
            viewModel.SetTabStripPlacementCommand.Execute("Top");
        }
    }

    private void OnTabPlacementRightClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.SetTabStripPlacementCommand.CanExecute("Right"))
        {
            viewModel.SetTabStripPlacementCommand.Execute("Right");
        }
    }

    private void OpenLogWindow_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AmiumStudioMainWindowViewModel viewModel)
        {
            return;
        }

        if (_logWindow is { } existingWindow)
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            existingWindow.Topmost = true;
            existingWindow.Topmost = false;
            return;
        }

        viewModel.RefreshLog();
        _logWindow = new LogWindow
        {
            DataContext = viewModel
        };
        _logWindow.Closed += OnLogWindowClosed;
        _logWindow.Show();
        _logWindow.Activate();
    }

    private void OnLogWindowClosed(object? sender, EventArgs e)
    {
        if (sender is LogWindow window)
        {
            window.Closed -= OnLogWindowClosed;
        }

        _logWindow = null;
    }

    private void HandleHostUiStateChanged(string action, BookProject? project)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not AmiumStudioMainWindowViewModel viewModel)
            {
                return;
            }

            switch (action)
            {
                case "Destroy":
                    viewModel.ApplyDestroyedUi(project);
                    break;
                case "Run" when project is not null:
                    viewModel.ApplyRunningUi(project);
                    break;
            }
        });
    }
}