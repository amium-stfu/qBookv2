using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public sealed partial class TextInputDialogWindow : Window
{
    private readonly TaskCompletionSource<string?> _tcs = new TaskCompletionSource<string?>();

    public TextInputDialogWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
        KeyDown += OnWindowKeyDown;
        var pad = this.FindControl<EditorTextInputPad>("TextInputPad")!;
        pad.KeyInvoked += OnPadKeyInvoked;
        pad.ActionInvoked += OnPadActionInvoked;
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    public void Initialize(MainWindowViewModel? viewModel, string header, string subHeader, string? initialValue)
        => Initialize(viewModel, header, subHeader, initialValue, false);

    public void Initialize(MainWindowViewModel? viewModel, string header, string subHeader, string? initialValue, bool isPassword)
    {
        if (viewModel is not null)
        {
            DataContext = viewModel;
        }

        this.FindControl<TextBlock>("HeaderTextBlock")!.Text = header;
        this.FindControl<TextBlock>("SubHeaderTextBlock")!.Text = subHeader;
        var input = this.FindControl<TextBox>("InputTextBox")!;
        input.Text = initialValue ?? string.Empty;
        input.PasswordChar = isPassword ? '•' : '\0';

        Dispatcher.UIThread.Post(() =>
        {
            var input = this.FindControl<TextBox>("InputTextBox");
            input?.Focus();
            input?.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnPadKeyInvoked(object? sender, string key)
    {
        var input = this.FindControl<TextBox>("InputTextBox")!;
        var text = input.Text ?? string.Empty;
        var caret = input.CaretIndex;
        if (caret < 0)
        {
            caret = 0;
        }
        if (caret > text.Length)
        {
            caret = text.Length;
        }

        var before = text.Substring(0, caret);
        var after = text.Substring(caret);
        input.Text = before + key + after;
        input.CaretIndex = caret + key.Length;
    }

    private void OnPadActionInvoked(object? sender, string action)
    {
        var input = this.FindControl<TextBox>("InputTextBox")!;
        switch (action)
        {
            case "Backspace":
                input.Text = string.Empty;
                input.CaretIndex = 0;
                input.SelectionStart = 0;
                input.SelectionEnd = 0;
                break;
            case "MoveLeft":
                {
                    var text = input.Text ?? string.Empty;
                    if (text.Length == 0)
                    {
                        break;
                    }

                    text = text[..^1];
                    input.Text = text;
                    var caret = text.Length;
                    input.CaretIndex = caret;
                    input.SelectionStart = caret;
                    input.SelectionEnd = caret;
                }
                break;
            case "Apply":
                Complete(input.Text ?? string.Empty);
                break;
            case "Cancel":
                Complete(null);
                break;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Complete(this.FindControl<TextBox>("InputTextBox")!.Text ?? string.Empty);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Complete(null);
            e.Handled = true;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.TrySetResult(null);
        }
    }

    private void Complete(string? value)
    {
        if (_tcs.Task.IsCompleted)
        {
            Close();
            return;
        }

        _tcs.TrySetResult(value);
        Close();
    }
}
