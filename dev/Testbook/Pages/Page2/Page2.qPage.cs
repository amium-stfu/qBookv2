using Amium.Host;
using Amium.Items;

namespace DefinitionPage2;

public class qPage : BookPage
{
    // Demo values used by the editable input controls on Page 2.
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
        // Publish the input demo items first so the page has stable runtime data.
        _numericAttached ??= Attach(_numericSource, "Input/Numeric");
        _integerAttached ??= Attach(_integerSource, "Input/Counter");
        _hexAttached ??= Attach(_hexSource, "Input/Hex");
        _bitmaskAttached ??= Attach(_bitmaskSource, "Input/Bits");
        _readOnlyAttached ??= Attach(_readOnlySource, "Input/Readonly");

        // Register the button demo commands individually so they are easy to inspect.
        PublishButtonCommands();
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
            PublishItem(_numericAttached);
        }

        if (_integerAttached is not null)
        {
            PublishItem(_integerAttached);
        }

        if (_hexAttached is not null)
        {
            PublishItem(_hexAttached);
        }

        if (_bitmaskAttached is not null)
        {
            PublishItem(_bitmaskAttached);
        }

        if (_readOnlyAttached is not null)
        {
            PublishItem(_readOnlyAttached);
        }
    }

    private void PublishButtonCommands()
    {
        PublishCommand(CreateStartCommand());
        PublishCommand(CreateResetCounterCommand());
        PublishCommand(CreateStopCommand());
    }

    // Raises the temperature, updates the hex register and enables status bits.
    private HostCommand CreateStartCommand()
    {
        return AttachCommand("Start", ExecuteStartCommand, "Start demo process");
    }

    // Clears the counter/register so users can immediately see a reset action.
    private HostCommand CreateResetCounterCommand()
    {
        return AttachCommand("ResetCounter", ExecuteResetCounterCommand, "Reset counter and register");
    }

    // Lowers the temperature and clears the run bit.
    private HostCommand CreateStopCommand()
    {
        return AttachCommand("Stop", ExecuteStopCommand, "Stop demo process");
    }

    private void ExecuteStartCommand()
    {
        _numericSource.Value = 24.8;
        _hexSource.Value = (ushort)0x2BCD;
        _bitmaskSource.Value = (ushort)(ToUInt16(_bitmaskSource.Value) | 0x0003);
        PublishAll();
    }

    private void ExecuteResetCounterCommand()
    {
        _integerSource.Value = 0;
        _hexSource.Value = (ushort)0x0000;
        PublishAll();
    }

    private void ExecuteStopCommand()
    {
        _numericSource.Value = 18.0;
        _bitmaskSource.Value = (ushort)(ToUInt16(_bitmaskSource.Value) & ~0x0002);
        PublishAll();
    }

    private static Item CreateDemoItem(string text, string path, string unit, object initialValue)
    {
        var item = new Item(name: text, path: path);
        item.Params["Text"].Value = text;
        item.Params["Unit"].Value = unit;
        item.Value = initialValue;
        return item;
    }

    private static ushort ToUInt16(object? value)
    {
        return value switch
        {
            ushort number => number,
            short number => unchecked((ushort)number),
            int number => unchecked((ushort)number),
            uint number => unchecked((ushort)number),
            long number => unchecked((ushort)number),
            ulong number => unchecked((ushort)number),
            _ => 0
        };
    }
}
