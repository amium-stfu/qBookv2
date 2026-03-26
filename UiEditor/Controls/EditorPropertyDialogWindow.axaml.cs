using System;
using Avalonia.Controls;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Controls;

public partial class EditorPropertyDialogWindow : Window
{
    private static EditorPropertyDialogWindow? _openInstance;

    public EditorPropertyDialogWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    public static EditorPropertyDialogWindow ShowOrActivate(Window? owner, object? dataContext)
    {
        if (_openInstance is not null)
        {
            _openInstance.DataContext = dataContext;
            _openInstance.Activate();
            return _openInstance;
        }

        var window = new EditorPropertyDialogWindow
        {
            DataContext = dataContext
        };

        _openInstance = window;
        if (owner is not null)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        return window;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_openInstance, this))
        {
            _openInstance = null;
        }

        if (DataContext is MainWindowViewModel { IsEditorDialogOpen: true } viewModel)
        {
            viewModel.CancelEditorDialog();
        }
    }
}