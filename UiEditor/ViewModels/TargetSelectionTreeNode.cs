using System.Collections.ObjectModel;

namespace Amium.UiEditor.ViewModels;

public sealed class TargetSelectionTreeNode
{
    public string DisplayName { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public bool IsSelectable { get; set; }

    public ObservableCollection<TargetSelectionTreeNode> Children { get; } = [];
}