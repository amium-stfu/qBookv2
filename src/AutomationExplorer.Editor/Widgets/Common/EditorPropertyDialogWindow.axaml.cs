using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class EditorPropertyDialogWindow : Window
{
    private static EditorPropertyDialogWindow? _openInstance;
    private MainWindowViewModel? _subscribedViewModel;

    public EditorPropertyDialogWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
        DataContextChanged += OnDataContextChanged;
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
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel = null;
        }

        if (ReferenceEquals(_openInstance, this))
        {
            _openInstance = null;
        }

        if (DataContext is MainWindowViewModel { IsEditorDialogOpen: true } viewModel)
        {
            viewModel.CancelEditorDialog();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedViewModel = null;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            _subscribedViewModel = vm;
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateWindowIcon(vm);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MainWindowViewModel vm && e.PropertyName == nameof(MainWindowViewModel.IsDarkTheme))
        {
            UpdateWindowIcon(vm);
        }
    }

    private void UpdateWindowIcon(MainWindowViewModel vm)
    {
        var iconName = vm.IsDarkTheme ? "cogDark.png" : "cogLight.png";
        var uri = new Uri($"avares://AutomationExplorer.Editor/EditorIcons/{iconName}");

        try
        {
            using var stream = AssetLoader.Open(uri);
            Icon = new WindowIcon(new Bitmap(stream));
        }
        catch
        {
            // If the PNGs are missing, just keep the default window icon.
        }
    }
}