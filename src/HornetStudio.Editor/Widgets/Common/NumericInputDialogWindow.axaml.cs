using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace HornetStudio.Editor.Widgets;

public sealed partial class NumericInputDialogWindow : Window
{
    private readonly TaskCompletionSource<double?> _tcs = new();
    private int _maxDecimalDigits;

    public NumericInputDialogWindow()
    {
        InitializeComponent();
        var pad = this.FindControl<EditorNumericInputPad>("NumericPad")!;
        pad.KeyInvoked += OnPadKeyInvoked;
        pad.ActionInvoked += OnPadActionInvoked;

        KeyDown += OnWindowKeyDown;
    }

    public Task<double?> WaitForResultAsync() => _tcs.Task;

    public void Initialize(string header, string subHeader, string? initialValue, string numericFormat)
        => Initialize(header, subHeader, initialValue, numericFormat, false);

    public void Initialize(string header, string subHeader, string? initialValue, string numericFormat, bool maskInput)
    {
        this.FindControl<TextBlock>("HeaderTextBlock")!.Text = header;
        this.FindControl<TextBlock>("SubHeaderTextBlock")!.Text = subHeader;
        var input = this.FindControl<TextBox>("InputTextBox")!;
        input.Text = initialValue ?? string.Empty;
        input.PasswordChar = maskInput ? '•' : '\0';

        _maxDecimalDigits = GetDecimalDigits(numericFormat);

        var allowDecimal = _maxDecimalDigits > 0;
        var allowSign = true;
        this.FindControl<EditorNumericInputPad>("NumericPad")!.Configure(allowDecimal, allowSign);

        Dispatcher.UIThread.Post(() =>
        {
            var box = this.FindControl<TextBox>("InputTextBox");
            box?.Focus();
            box?.SelectAll();
        }, DispatcherPriority.Input);
    }

    private static int GetDecimalDigits(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "0.##";
        }

        var decimalIndex = pattern.IndexOf('.', StringComparison.Ordinal);
        return decimalIndex < 0 ? 0 : pattern[(decimalIndex + 1)..].Count(ch => ch is '0' or '#');
    }

    private void OnPadKeyInvoked(object? sender, string key)
    {
        var input = this.FindControl<TextBox>("InputTextBox")!;
        var text = input.Text ?? string.Empty;

        if (key == ".")
        {
            if (_maxDecimalDigits <= 0 || text.Contains('.', StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(text) || text == "-")
            {
                text += "0.";
            }
            else
            {
                text += ".";
            }

            input.Text = text;
            input.CaretIndex = text.Length;
            return;
        }

        if (key == "+/-")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                input.Text = "-";
            }
            else if (text.StartsWith("-", StringComparison.Ordinal))
            {
                input.Text = text[1..];
            }
            else
            {
                input.Text = "-" + text;
            }

            input.CaretIndex = input.Text.Length;
            return;
        }

        if (text == "0")
        {
            input.Text = key;
        }
        else if (text == "-0")
        {
            input.Text = "-" + key;
        }
        else
        {
            input.Text = text + key;
        }

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

    private static double? ParseValueOrNull(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private void Complete(double? value)
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
