using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Amium.EditorUi.Controls;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class EditorSqlLoggerControl : EditorTemplateWidget
{
    private FolderItemModel? Item => DataContext as FolderItemModel;
    private bool _isRecording;

    public EditorSqlLoggerControl()
    {
        InitializeComponent();
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleInteractivePointerPressed(e);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        HandleSettingsClicked(e);
    }

    private void OnToggleRecordingClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window { DataContext: MainWindowViewModel viewModel })
        {
            return;
        }

        if (_isRecording)
        {
            viewModel.StopSqlLogging(Item);
            _isRecording = false;
        }
        else
        {
            viewModel.StartSqlLogging(Item);
            _isRecording = true;
        }

        UpdateRecordingVisualState(_isRecording);
    }

    private void UpdateRecordingVisualState(bool isRecording)
    {
        if (this.FindControl<TextBlock>("HeaderStateText") is { } headerStateText
            && this.FindControl<Border>("HeaderStateBorder") is { } headerBorder)
        {
            headerStateText.Text = isRecording ? "Stop" : "Start";
            headerBorder.Background = isRecording
                ? new SolidColorBrush(Color.Parse("#DC2626")) // red
                : new SolidColorBrush(Color.Parse("#111827")); // default dark
        }
    }

    private void OnOpenFolderClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is null)
        {
            return;
        }

        var directory = string.IsNullOrWhiteSpace(Item.CsvDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AmiumLogs")
            : Item.CsvDirectory.Trim();

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{directory}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // ignored
        }

        e.Handled = true;
    }
}
