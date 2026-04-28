using System;
using System.ComponentModel;
using Avalonia.Controls;
using HornetStudio.Editor.Models;
using HornetStudio.ViewModels;

namespace HornetStudio;

public partial class LogWindow : Window
{
    private MainWindowViewModel? _observedViewModel;

    public LogWindow()
    {
        InitializeComponent();
        HostLogItem = new FolderItemModel
        {
            Kind = ControlKind.LogControl,
            Name = "HostLogWindow",
            Title = "Host ProcessLog",
            Footer = "Logs.Host",
            TargetLog = "Logs.Host",
            Width = 860,
            Height = 580
        };

        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        SizeChanged += OnSizeChanged;
        Closed += OnClosed;
    }

    public FolderItemModel HostLogItem { get; }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyTheme();
        UpdateControlSize();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        UnhookViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        HookViewModel();
        ApplyTheme();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateControlSize();
    }

    private void HookViewModel()
    {
        if (ReferenceEquals(_observedViewModel, DataContext))
        {
            return;
        }

        UnhookViewModel();
        _observedViewModel = DataContext as MainWindowViewModel;
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void UnhookViewModel()
    {
        if (_observedViewModel is null)
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsDarkTheme))
        {
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        HostLogItem.ApplyTheme(_observedViewModel?.IsDarkTheme ?? false);
    }

    private void UpdateControlSize()
    {
        HostLogItem.Width = Math.Max(320, ClientSize.Width - 24);
        HostLogItem.Height = Math.Max(220, ClientSize.Height - 24);
    }
}