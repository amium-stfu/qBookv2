using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Amium.Host;
using UdlBook.ViewModels;

namespace UdlBook;

public partial class MainWindow : Window
{
    private LogWindow? _logWindow;

    public MainWindow()
    {
        InitializeComponent();
        Core.UiStateChanged += HandleHostUiStateChanged;
    }

    private async void NewBook_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Create layout file",
            SuggestedFileName = "Page.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON layout")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file is null)
        {
            return;
        }

        var path = file.Path.LocalPath;

                if (!File.Exists(path))
                {
                        var content = @"{
    ""Page"": ""Page1"",
    ""Title"": ""New Layout"",
    ""Layout"": {
        ""Type"": ""Canvas"",
        ""Children"": []
    }
}";

                        File.WriteAllText(path, content);
                }

        viewModel.LoadLayoutFromFile(path);
    }

    private async void LoadBook_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        // Stop all runtime activities before loading a new book.
        Amium.Host.TasksManager.StopAll();
        Amium.Host.ThreadsManager.StopAll();
        Amium.Host.TimerManager.StopAll();

        var file = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Book.json",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Book definition")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file.Count == 0)
        {
            return;
        }

        var selectedPath = file[0].Path.LocalPath;
        // Pass the selected file path to the host; it can resolve the
        // actual project root (it handles both file and directory paths).
        viewModel.BookProjectPath = selectedPath;
        if (viewModel.LoadBookCommand.CanExecute(null))
        {
            viewModel.LoadBookCommand.Execute(null);
        }
    }

    private void SaveLayout_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SaveCurrentLayout();
    }

    private void SetStartLayout_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SetCurrentLayoutAsStartup();
    }

    private async void SaveLayoutAs_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save layout as",
            SuggestedFileName = "Page.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("JSON layout")
                {
                    Patterns = new[] { "*.json" }
                }
            }
        });

        if (file is null)
        {
            return;
        }

        var path = file.Path.LocalPath;
        viewModel.SaveCurrentLayoutAs(path);
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
        if (DataContext is not MainWindowViewModel viewModel)
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

    private void ToggleTheme_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.IsDarkTheme = !viewModel.IsDarkTheme;
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
            if (DataContext is not MainWindowViewModel viewModel)
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