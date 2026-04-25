using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Widgets;

public partial class CachedFolderHostControl : UserControl
{
    private readonly Dictionary<FolderModel, FolderEditorWidget> _folderEditors = [];
    private MainWindowViewModel? _viewModel;

    public CachedFolderHostControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => AttachToViewModel(null);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachToViewModel(DataContext as MainWindowViewModel);
    }

    private void AttachToViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Folders.CollectionChanged -= OnFoldersCollectionChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Folders.CollectionChanged += OnFoldersCollectionChanged;
        }

        ReconcileFolderEditors();
        EnsureFolderEditors();
        UpdateVisibleFolder();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedFolder))
        {
            EnsureFolderEditors();
            UpdateVisibleFolder();
        }
    }

    private void OnFoldersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReconcileFolderEditors();
        EnsureFolderEditors();
        UpdateVisibleFolder();
    }

    private void ReconcileFolderEditors()
    {
        if (_viewModel is null)
        {
            _folderEditors.Clear();
            HostGrid.Children.Clear();
            return;
        }

        var activeFolders = _viewModel.Folders.ToHashSet();
        var removedFolders = _folderEditors.Keys.Where(folder => !activeFolders.Contains(folder)).ToList();
        foreach (var folder in removedFolders)
        {
            if (_folderEditors.Remove(folder, out var editor))
            {
                HostGrid.Children.Remove(editor);
            }
        }
    }

    private void EnsureFolderEditors()
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var folder in _viewModel.Folders)
        {
            if (_folderEditors.ContainsKey(folder))
            {
                continue;
            }

            var editor = new FolderEditorWidget
            {
                DataContext = _viewModel,
                Folder = folder,
                IsVisible = true,
                IsHitTestVisible = false,
                Opacity = 0
            };

            _folderEditors[folder] = editor;
            HostGrid.Children.Add(editor);
        }
    }

    private void UpdateVisibleFolder()
    {
        var selectedFolder = _viewModel?.SelectedFolder;
        foreach (var pair in _folderEditors)
        {
            var isActive = ReferenceEquals(pair.Key, selectedFolder);
            pair.Value.IsVisible = true;
            pair.Value.IsHitTestVisible = isActive;
            pair.Value.Opacity = isActive ? 1 : 0;
            pair.Value.ZIndex = isActive ? 1 : 0;
        }
    }
}

public partial class CachedFolderHostWidget : CachedFolderHostControl
{
}