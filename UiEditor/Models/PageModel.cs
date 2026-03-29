using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Amium.Host;
using Amium.UiEditor.ViewModels;

namespace Amium.UiEditor.Models;

public sealed class PageModel : ObservableObject
{
    private bool _isSelected;

    public PageModel()
    {
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public int Index { get; init; }
    public Dictionary<int, string> Views { get; init; } = new();
    public string Name { get; init; } = string.Empty;
    public string DisplayText { get; init; } = string.Empty;

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

    public string ItemSummary => $"{Items.Count} Controls";

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RaisePropertyChanged(nameof(ItemSummary));
}
