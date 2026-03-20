using System.Collections.ObjectModel;
using System.Collections.Specialized;
using UiEditor.Host;
using UiEditor.ViewModels;

namespace UiEditor.Models;

public sealed class PageModel : ObservableObject
{
    private bool _isSelected;

    public PageModel()
    {
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public int Index { get; init; }

    public string Name { get; init; } = string.Empty;

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
                OnPropertyChanged(nameof(ItemSummary));
            }
        }
    }

    public string ItemSummary => $"{Items.Count} Controls";

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RaisePropertyChanged(nameof(ItemSummary));
}
