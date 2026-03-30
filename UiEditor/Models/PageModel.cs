using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Amium.Host;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Models;

public sealed class PageModel : ObservableObject
{
    private bool _isSelected;
    private int _actualViewId = 1;

    public PageModel()
    {
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public int Index { get; init; }
    public Dictionary<int, string> Views { get; init; } = new();

    public int ActualViewId
    {
        get => _actualViewId;
        set
        {
            if (SetProperty(ref _actualViewId, value))
            {
                ApplyActiveViewToItems(_actualViewId);
                OnPropertyChanged(nameof(CurrentViewCaption));
            }
        }
    }

    public string Name { get; init; } = string.Empty;

    private string _displayText = string.Empty;

    public string DisplayText
    {
        get => _displayText;
        set
        {
            if (SetProperty(ref _displayText, value))
            {
                OnPropertyChanged(nameof(TabTitle));
            }
        }
    }

    public string? UiFilePath { get; init; }

    public BookUiPageLayout? UiLayoutDefinition { get; init; }

    public ObservableCollection<PageItemModel> Items { get; } = [];

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(IsNotSelected));
                OnPropertyChanged(nameof(ItemSummary));
            }
        }
    }

    public bool IsNotSelected => !_isSelected;

    public string TabTitle => string.IsNullOrWhiteSpace(DisplayText) ? Name : DisplayText;

    public string CurrentViewCaption
    {
        get
        {
            if (Views is { Count: > 0 } && Views.TryGetValue(ActualViewId, out var caption))
            {
                return caption;
            }

            return string.Empty;
        }
    }

    public string ItemSummary => $"{Items.Count} Controls";

    public void UpdateViewCaption(int id, string caption)
    {
        if (id <= 0)
        {
            return;
        }

        Views[id] = caption;

        if (id == ActualViewId)
        {
            OnPropertyChanged(nameof(CurrentViewCaption));
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyActiveViewToItems(ActualViewId);
        RaisePropertyChanged(nameof(ItemSummary));
    }

    private void ApplyActiveViewToItems(int activeViewId)
    {
        foreach (var item in Items)
        {
            item.ApplyActiveView(activeViewId);
        }
    }
}
