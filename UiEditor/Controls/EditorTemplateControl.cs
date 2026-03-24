using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Amium.EditorUi.Controls;

public partial class EditorTemplateControl : UserControl
{
    public static readonly StyledProperty<object?> BodyContentProperty =
        AvaloniaProperty.Register<EditorTemplateControl, object?>(nameof(BodyContent));

    public static readonly StyledProperty<object?> HeaderActionsContentProperty =
        AvaloniaProperty.Register<EditorTemplateControl, object?>(nameof(HeaderActionsContent));

    public static readonly DirectProperty<EditorTemplateControl, bool> HostIsEditModeProperty =
        AvaloniaProperty.RegisterDirect<EditorTemplateControl, bool>(nameof(HostIsEditMode), control => control.HostIsEditMode);

    public static readonly DirectProperty<EditorTemplateControl, string?> HostPrimaryTextBrushProperty =
        AvaloniaProperty.RegisterDirect<EditorTemplateControl, string?>(nameof(HostPrimaryTextBrush), control => control.HostPrimaryTextBrush);

    private IEditorUiHost? _host;
    private INotifyPropertyChanged? _hostNotifier;
    private bool _hostIsEditMode;
    private string? _hostPrimaryTextBrush = "#111827";

    public EditorTemplateControl()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
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

    public bool HostIsEditMode
    {
        get => _hostIsEditMode;
        private set => SetAndRaise(HostIsEditModeProperty, ref _hostIsEditMode, value);
    }

    public string? HostPrimaryTextBrush
    {
        get => _hostPrimaryTextBrush;
        private set => SetAndRaise(HostPrimaryTextBrushProperty, ref _hostPrimaryTextBrush, value);
    }

    protected object? ItemContext => DataContext;

    protected IEditorUiHost? Host => _host;

    protected void HandleInteractivePointerPressed(PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    protected void HandleSettingsClicked(RoutedEventArgs e)
    {
        if (ItemContext is null || Host is null)
        {
            return;
        }

        var anchorTarget = this.GetVisualRoot() as Visual;
        var anchor = anchorTarget is null
            ? new Point(24, 24)
            : this.TranslatePoint(new Point(Bounds.Width + 8, 0), anchorTarget) ?? new Point(24, 24);

        Host.OpenItemEditor(ItemContext, anchor.X, anchor.Y);
        e.Handled = true;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        ResolveHost();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachToHost(null);
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
        if (ItemContext is null || Host is null)
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

        Host.DeleteItem(ItemContext);
        e.Handled = true;
    }

    private void ResolveHost()
    {
        var resolved = this.GetVisualRoot() is TopLevel { DataContext: IEditorUiHost host }
            ? host
            : null;

        AttachToHost(resolved);
        RefreshHostBindings();
    }

    private void AttachToHost(IEditorUiHost? host)
    {
        if (ReferenceEquals(_host, host))
        {
            return;
        }

        if (_hostNotifier is not null)
        {
            _hostNotifier.PropertyChanged -= OnHostPropertyChanged;
        }

        _host = host;
        _hostNotifier = host as INotifyPropertyChanged;

        if (_hostNotifier is not null)
        {
            _hostNotifier.PropertyChanged += OnHostPropertyChanged;
        }
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)
            || e.PropertyName == nameof(IEditorUiHost.IsEditMode)
            || e.PropertyName == nameof(IEditorUiHost.PrimaryTextBrush))
        {
            RefreshHostBindings();
        }
    }

    private void RefreshHostBindings()
    {
        HostIsEditMode = _host?.IsEditMode ?? false;
        HostPrimaryTextBrush = string.IsNullOrWhiteSpace(_host?.PrimaryTextBrush) ? "#111827" : _host.PrimaryTextBrush;
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
