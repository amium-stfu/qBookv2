using UiEditor.Host;
using UiEditor.Items;

namespace DefinitionPage2;

public class qPage : BookPage
{
    private readonly Item _numericSource = CreateDemoItem("Editable Temperature", "Runtime/Page2/Input", "degC", 21.5);
    private readonly Item _integerSource = CreateDemoItem("Editable Counter", "Runtime/Page2/Input", "pcs", 42);
    private readonly Item _hexSource = CreateDemoItem("Editable Register", "Runtime/Page2/Input", "hex", (ushort)0x1A2B);
    private readonly Item _bitmaskSource = CreateDemoItem("Editable Status Word", "Runtime/Page2/Input", "mask", (ushort)0x0005);
    private readonly Item _readOnlySource = CreateDemoItem("Readonly Snapshot", "Runtime/Page2/Input", "bar", 6.2);

    private Item? _numericAttached;
    private Item? _integerAttached;
    private Item? _hexAttached;
    private Item? _bitmaskAttached;
    private Item? _readOnlyAttached;

    public qPage() : base("Page2")
    {
    }

    protected override void OnInitialize()
    {
        _numericAttached ??= Attach(_numericSource, "Input/Numeric");
        _integerAttached ??= Attach(_integerSource, "Input/Counter");
        _hexAttached ??= Attach(_hexSource, "Input/Hex");
        _bitmaskAttached ??= Attach(_bitmaskSource, "Input/Bits");
        _readOnlyAttached ??= Attach(_readOnlySource, "Input/Readonly");
    }

    protected override void OnRun()
    {
        PublishAll();
    }

    protected override void OnDestroy()
    {
    }

    private void PublishAll()
    {
        if (_numericAttached is not null)
        {
            UiPublisher.Publish(_numericAttached);
        }

        if (_integerAttached is not null)
        {
            UiPublisher.Publish(_integerAttached);
        }

        if (_hexAttached is not null)
        {
            UiPublisher.Publish(_hexAttached);
        }

        if (_bitmaskAttached is not null)
        {
            UiPublisher.Publish(_bitmaskAttached);
        }

        if (_readOnlyAttached is not null)
        {
            UiPublisher.Publish(_readOnlyAttached);
        }
    }

    private static Item CreateDemoItem(string text, string path, string unit, object initialValue)
    {
        var item = new Item(name: text, path: path);
        item.Params["Text"].Value = text;
        item.Params["Unit"].Value = unit;
        item.Value = initialValue;
        return item;
    }
}
