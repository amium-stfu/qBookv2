namespace UiEditor.ViewModels;

public sealed class SelectionState : ObservableObject
{
    private double _startX;
    private double _startY;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private bool _isSelecting;
    private bool _showPicker;
    private double _popupX;
    private double _popupY;
    private bool _showListPicker;
    private double _listPopupX;
    private double _listPopupY;

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

    public bool ShowPicker
    {
        get => _showPicker;
        set => SetProperty(ref _showPicker, value);
    }

    public double PopupX
    {
        get => _popupX;
        set => SetProperty(ref _popupX, value);
    }

    public double PopupY
    {
        get => _popupY;
        set => SetProperty(ref _popupY, value);
    }

    public bool ShowListPicker
    {
        get => _showListPicker;
        set => SetProperty(ref _showListPicker, value);
    }

    public double ListPopupX
    {
        get => _listPopupX;
        set => SetProperty(ref _listPopupX, value);
    }

    public double ListPopupY
    {
        get => _listPopupY;
        set => SetProperty(ref _listPopupY, value);
    }
}
