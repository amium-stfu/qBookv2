using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Amium.UiEditor.Models;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Controls;

public partial class CachedPageHostControl : UserControl
{
    private readonly Dictionary<PageModel, PageEditorControl> _pageEditors = [];
    private MainWindowViewModel? _viewModel;

    public CachedPageHostControl()
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
            _viewModel.Pages.CollectionChanged -= OnPagesCollectionChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Pages.CollectionChanged += OnPagesCollectionChanged;
        }

        ReconcilePageEditors();
        EnsureSelectedPageEditor();
        UpdateVisiblePage();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedPage))
        {
            EnsureSelectedPageEditor();
            UpdateVisiblePage();
        }
    }

    private void OnPagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReconcilePageEditors();
        EnsureSelectedPageEditor();
        UpdateVisiblePage();
    }

    private void ReconcilePageEditors()
    {
        if (_viewModel is null)
        {
            _pageEditors.Clear();
            HostGrid.Children.Clear();
            return;
        }

        var activePages = _viewModel.Pages.ToHashSet();
        var removedPages = _pageEditors.Keys.Where(page => !activePages.Contains(page)).ToList();
        foreach (var page in removedPages)
        {
            if (_pageEditors.Remove(page, out var editor))
            {
                HostGrid.Children.Remove(editor);
            }
        }
    }

    private void EnsureSelectedPageEditor()
    {
        if (_viewModel?.SelectedPage is not { } selectedPage)
        {
            return;
        }

        if (_pageEditors.ContainsKey(selectedPage))
        {
            return;
        }

        var editor = new PageEditorControl
        {
            DataContext = _viewModel,
            Page = selectedPage,
            IsVisible = false
        };

        _pageEditors[selectedPage] = editor;
        HostGrid.Children.Add(editor);
    }

    private void UpdateVisiblePage()
    {
        var selectedPage = _viewModel?.SelectedPage;
        foreach (var pair in _pageEditors)
        {
            var isActive = ReferenceEquals(pair.Key, selectedPage);
            pair.Value.IsVisible = isActive;
            pair.Value.ZIndex = isActive ? 1 : 0;
        }
    }
}