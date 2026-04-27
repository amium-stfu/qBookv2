using System.Collections.Generic;

namespace HornetStudio.ViewModels;

public sealed class HornetStudioPageViewModel : ObservableObject
{
    private bool _isSelected;

    public HornetStudioPageViewModel(
        int index,
        string name,
        string tabTitle,
        string description,
        string summaryTitle,
        string summaryText,
        string sideLabel,
        bool showPlaceholderCard,
        string placeholderTitle,
        string placeholderText,
        IReadOnlyList<string> bulletPoints,
        string footerNote)
    {
        Index = index;
        Name = name;
        TabTitle = tabTitle;
        Description = description;
        SummaryTitle = summaryTitle;
        SummaryText = summaryText;
        SideLabel = sideLabel;
        ShowPlaceholderCard = showPlaceholderCard;
        PlaceholderTitle = placeholderTitle;
        PlaceholderText = placeholderText;
        BulletPoints = bulletPoints;
        FooterNote = footerNote;
    }

    public int Index { get; }

    public string Name { get; }

    public string TabTitle { get; }

    public string Caption => Description;

    public string Description { get; }

    public string SummaryTitle { get; }

    public string SummaryText { get; }

    public string SideLabel { get; }

    public bool ShowPlaceholderCard { get; }

    public string PlaceholderTitle { get; }

    public string PlaceholderText { get; }

    public IReadOnlyList<string> BulletPoints { get; }

    public string FooterNote { get; }

    public bool ShowsUdlClientBadge => Name == "UdlClient";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(IsNotSelected));
            }
        }
    }

    public bool IsNotSelected => !IsSelected;
}