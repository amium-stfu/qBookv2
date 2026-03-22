using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Controls;

public partial class EditorTemplateControl : UserControl
{
    public static readonly StyledProperty<object?> BodyContentProperty =
        AvaloniaProperty.Register<EditorTemplateControl, object?>(nameof(BodyContent));

    public static readonly StyledProperty<object?> HeaderActionsContentProperty =
        AvaloniaProperty.Register<EditorTemplateControl, object?>(nameof(HeaderActionsContent));

    public EditorTemplateControl()
    {
        InitializeComponent();
    }

    public object? BodyContent
    {
        get => GetValue(BodyContentProperty);
        set => SetValue(BodyContentProperty, value);
    }

    public object? HeaderActionsContent
    {
        get => GetValue(HeaderActionsContentProperty);
        set => SetValue(HeaderActionsContentProperty, value);
    }

    protected PageItemModel? Item => DataContext as PageItemModel;

    protected MainWindowViewModel? ViewModel
        => this.GetVisualRoot() is Window { DataContext: MainWindowViewModel viewModel } ? viewModel : null;

    protected void HandleInteractivePointerPressed(PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    protected void HandleSettingsClicked(RoutedEventArgs e)
    {
        if (Item is null || ViewModel is null || this.GetVisualAncestors().OfType<PageEditorControl>().FirstOrDefault() is not { } editor)
        {
            return;
        }

        var anchor = this.TranslatePoint(new Point(Bounds.Width + 8, 0), editor) ?? new Point(24, 24);
        ViewModel.OpenItemEditor(Item, anchor.X, anchor.Y);
        e.Handled = true;
    }

    private void OnInteractivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        HandleInteractivePointerPressed(e);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        HandleSettingsClicked(e);
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (Item is null || ViewModel is null)
        {
            return;
        }

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null)
        {
            return;
        }

        var confirmed = await ShowDeleteDialogAsync(owner);
        if (!confirmed)
        {
            e.Handled = true;
            return;
        }

        ViewModel.DeleteItem(Item);
        e.Handled = true;
    }

    private static async Task<bool> ShowDeleteDialogAsync(Window owner)
    {
        var dialog = new Window
        {
            Title = "Löschen",
            Width = 320,
            Height = 140,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.Full
        };

        var text = new TextBlock
        {
            Text = "Wirklich löschen?",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var yesButton = new Button
        {
            Content = "Ja",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var noButton = new Button
        {
            Content = "Nein",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        yesButton.Click += (_, _) => dialog.Close(true);
        noButton.Click += (_, _) => dialog.Close(false);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { noButton, yesButton }
        };

        dialog.Content = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                text,
                buttonPanel
            }
        };

        Grid.SetRow(text, 0);
        Grid.SetRow(buttonPanel, 1);

        return await dialog.ShowDialog<bool>(owner);
    }
}
