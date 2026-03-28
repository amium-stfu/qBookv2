using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Amium.UiEditor.Widgets;

public sealed partial class HexInputDialogWindow : Window
{
    private readonly TaskCompletionSource<ulong?> _tcs = new();
    private int _maxDigits = 16;

    public HexInputDialogWindow()
    {
        InitializeComponent();
        var pad = this.FindControl<EditorHexInputPad>("HexPad")!;
        pad.KeyInvoked += OnPadKeyInvoked;
        pad.ActionInvoked += OnPadActionInvoked;

        KeyDown += OnWindowKeyDown;
    }

    public Task<ulong?> WaitForResultAsync() => _tcs.Task;

    public void Initialize(string header, string subHeader, ulong? initialValue, int digits)
    {
        if (digits > 0)
        {
            _maxDigits = digits;
        }

        this.FindControl<TextBlock>("HeaderTextBlock")!.Text = header;
        this.FindControl<TextBlock>("SubHeaderTextBlock")!.Text = subHeader;
        var input = this.FindControl<TextBox>("InputTextBox")!;
        input.Text = initialValue.HasValue
            ? initialValue.Value.ToString($"X{_maxDigits}", CultureInfo.InvariantCulture)
            : string.Empty;

        Dispatcher.UIThread.Post(() =>
        {
            var box = this.FindControl<TextBox>("InputTextBox");
            box?.Focus();
            box?.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnPadKeyInvoked(object? sender, string key)
    {
        var input = this.FindControl<TextBox>("InputTextBox")!;
        var text = input.Text ?? string.Empty;

        if (text.Length >= _maxDigits)
        {
            return;
        }

        input.Text = text + key.ToUpperInvariant();
        input.CaretIndex = input.Text.Length;
    }

    private void OnPadActionInvoked(object? sender, string action)
    {
        var input = this.FindControl<TextBox>("InputTextBox")!;
        switch (action)
        {
            case "DEL":
                if (!string.IsNullOrEmpty(input.Text))
                {
                    input.Text = input.Text[..^1];
                    input.CaretIndex = input.Text.Length;
                }
                break;
            case "Clear":
                input.Text = string.Empty;
                input.CaretIndex = 0;
                break;
            case "Cancel":
                Complete(null);
                break;
            case "OK":
                Complete(ParseValueOrNull(input.Text));
                break;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Complete(ParseValueOrNull(this.FindControl<TextBox>("InputTextBox")!.Text));
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Complete(null);
            e.Handled = true;
        }
    }

    private static ulong? ParseValueOrNull(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private void Complete(ulong? value)
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
