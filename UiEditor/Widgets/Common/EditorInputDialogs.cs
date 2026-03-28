using System.Threading.Tasks;
using Avalonia.Controls;

namespace Amium.UiEditor.Widgets;

public static class EditorInputDialogs
{
    public static async Task<string?> EditTextAsync(Window owner, string header, string subHeader, string? initialValue = null)
    {
        var dialog = new TextInputDialogWindow();
        dialog.Initialize(owner.DataContext as Amium.UiEditor.ViewModels.MainWindowViewModel, header, subHeader, initialValue);
        await dialog.ShowDialog(owner);
        return await dialog.WaitForResultAsync();
    }

    public static async Task<string?> EditTextAsync(Window owner, string header, string subHeader, string? initialValue, bool isPassword)
    {
        var dialog = new TextInputDialogWindow();
        dialog.Initialize(owner.DataContext as Amium.UiEditor.ViewModels.MainWindowViewModel, header, subHeader, initialValue, isPassword);
        await dialog.ShowDialog(owner);
        return await dialog.WaitForResultAsync();
    }

    public static async Task<double?> EditNumericAsync(Window owner, string header, string subHeader, string format = "0.##", double? initialValue = null)
    {
        var dialog = new NumericInputDialogWindow();
        dialog.DataContext = owner.DataContext;
        dialog.Initialize(header, subHeader, initialValue?.ToString(format, System.Globalization.CultureInfo.InvariantCulture), format);
        await dialog.ShowDialog(owner);
        return await dialog.WaitForResultAsync();
    }

    public static async Task<double?> EditNumericAsync(Window owner, string header, string subHeader, string format, double? initialValue, bool maskInput)
    {
        var dialog = new NumericInputDialogWindow();
        dialog.DataContext = owner.DataContext;
        dialog.Initialize(header, subHeader, initialValue?.ToString(format, System.Globalization.CultureInfo.InvariantCulture), format, maskInput);
        await dialog.ShowDialog(owner);
        return await dialog.WaitForResultAsync();
    }

    public static async Task<ulong?> EditHexAsync(Window owner, string header, string subHeader, int digits = 8, ulong? initialValue = null)
    {
        var dialog = new HexInputDialogWindow();
        dialog.DataContext = owner.DataContext;
        dialog.Initialize(header, subHeader, initialValue, digits);
        await dialog.ShowDialog(owner);
        return await dialog.WaitForResultAsync();
    }
}
