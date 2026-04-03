using System.Collections.ObjectModel;

namespace Amium.UiEditor.ViewModels;

public sealed class ChartSeriesEditorRow : ObservableObject
{
    private string _targetPath = string.Empty;
    private string _targetName = string.Empty;
    private string _axis = "Y1";
    private string _style = "Line";

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value ?? string.Empty);
    }

    public string TargetName
    {
        get => _targetName;
        set => SetProperty(ref _targetName, value ?? string.Empty);
    }

    public string Axis
    {
        get => _axis;
        set => SetProperty(ref _axis, string.IsNullOrWhiteSpace(value) ? "Y1" : value);
    }

    public string Style
    {
        get => _style;
        set => SetProperty(ref _style, string.IsNullOrWhiteSpace(value) ? "Line" : value);
    }

    public ObservableCollection<string> TargetOptions { get; } = [];

    public ObservableCollection<string> AxisOptions { get; } = [];

    public ObservableCollection<string> StyleOptions { get; } = [];
}
