using Avalonia;
using Amium.UiEditor.Controls;

namespace Amium.UiEditor.Widgets;

public partial class EditorProcessLogControl : EditorTemplateWidget
{
    public static readonly StyledProperty<bool> PageIsActiveProperty =
        AvaloniaProperty.Register<EditorProcessLogControl, bool>(nameof(PageIsActive), true);

    public bool PageIsActive
    {
        get => GetValue(PageIsActiveProperty);
        set => SetValue(PageIsActiveProperty, value);
    }

    public EditorProcessLogControl()
    {
        InitializeComponent();
    }
}

