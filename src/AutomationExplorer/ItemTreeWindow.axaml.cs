using System;
using System.ComponentModel;
using Avalonia.Controls;
using AutomationExplorer.ViewModels;

namespace AutomationExplorer;

public partial class ItemTreeWindow : Window
{
    private readonly ItemTreeWindowViewModel _viewModel;

    public ItemTreeWindow()
    {
        InitializeComponent();
        _viewModel = new ItemTreeWindowViewModel();
        DataContext = _viewModel;
        Closed += OnClosed;
    }

    public ItemTreeWindow(MainWindowViewModel hostViewModel, Amium.UiEditor.Models.FolderModel folder)
        : this()
    {
        Attach(hostViewModel, folder);
    }

    public void Attach(MainWindowViewModel hostViewModel, Amium.UiEditor.Models.FolderModel folder)
    {
        _viewModel.Attach(hostViewModel);
        _viewModel.SetFolder(folder);
    }

    public void AttachToProject(MainWindowViewModel hostViewModel)
    {
        _viewModel.Attach(hostViewModel);
        _viewModel.ShowProjectScope();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Closed -= OnClosed;
        _viewModel.Dispose();
    }
}
