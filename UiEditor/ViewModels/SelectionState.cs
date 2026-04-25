namespace Amium.UiEditor.ViewModels;

public sealed class SelectionState : ObservableObject
{
    private double _startX;
    private double _startY;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private bool _isSelecting;

    public double StartX
    {
        get => _startX;
        set => SetProperty(ref _startX, value);
    }

    public double StartY
    {
        get => _startY;
        set => SetProperty(ref _startY, value);
    }

    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public bool IsSelecting
    {
        get => _isSelecting;
        set => SetProperty(ref _isSelecting, value);
    }
}
