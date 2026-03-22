using System.Collections.ObjectModel;

namespace Amium.UiEditor.ViewModels;

public sealed class ChartSeriesEditorRow : ObservableObject
{
    private string _targetPath = string.Empty;
    private string _axis = "Y1";

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value ?? string.Empty);
    }

    public string Axis
    {
        get => _axis;
        set => SetProperty(ref _axis, string.IsNullOrWhiteSpace(value) ? "Y1" : value);
    }

    public ObservableCollection<string> TargetOptions { get; } = [];

    public ObservableCollection<string> AxisOptions { get; } = [];
}
