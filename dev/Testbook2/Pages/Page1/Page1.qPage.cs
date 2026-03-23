using Amium.Host;
using Amium.Items;

namespace DefinitionPage1;

public class qPage : BookPage
{
    private readonly Item _floatSource = CreateDemoItem("Float Sensor", "Runtime/Page1/Float", "bar", 12.75);
    private readonly Item _textSource = CreateDemoItem("Status Text", "Runtime/Page1/Text", string.Empty, "Ready");
    private readonly Item _boolSource = CreateDemoItem("Drive Enabled", "Runtime/Page1/Bool", string.Empty, true);
    private readonly Item _bitsSource = CreateDemoItem("Output Mask", "Runtime/Page1/Bits", string.Empty, (ushort)0b1010_0110);

    private Item? _floatAttached;
    private Item? _textAttached;
    private Item? _boolAttached;
    private Item? _bitsAttached;

    public qPage() : base("Page1")
    {
    }

    protected override void OnInitialize()
    {
        _floatAttached ??= Attach(_floatSource, "Demo/Float");
        _textAttached ??= Attach(_textSource, "Demo/Text");
        _boolAttached ??= Attach(_boolSource, "Demo/Bool");
        _bitsAttached ??= Attach(_bitsSource, "Demo/Bits");

        PublishAll();
        PublishCommands();
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
        if (_floatAttached is not null)
        {
            PublishItem(_floatAttached);
        }

        if (_textAttached is not null)
        {
            PublishItem(_textAttached);
        }

        if (_boolAttached is not null)
        {
            PublishItem(_boolAttached);
        }

        if (_bitsAttached is not null)
        {
            PublishItem(_bitsAttached);
        }
    }

    private void PublishCommands()
    {
        PublishCommand("Pulse", ExecutePulse, "Set demo values to active");
        PublishCommand("Reset", ExecuteReset, "Reset demo values");
        PublishCommand("Toggle", ExecuteToggle, "Toggle bool and bits");
        PublishCommand("Standby", ExecuteStandby, "Set demo values to standby");
    }

    private void ExecutePulse()
    {
        _floatSource.Value = 18.4;
        _textSource.Value = "Boost";
        _boolSource.Value = true;
        _bitsSource.Value = (ushort)0b1111_0000;
        PublishAll();
    }

    private void ExecuteReset()
    {
        _floatSource.Value = 12.75;
        _textSource.Value = "Ready";
        _boolSource.Value = true;
        _bitsSource.Value = (ushort)0b1010_0110;
        PublishAll();
    }

    private void ExecuteToggle()
    {
        var currentBool = _boolSource.Value is bool boolValue && boolValue;
        _boolSource.Value = !currentBool;

        var currentBits = _bitsSource.Value is ushort ushortValue ? ushortValue : (ushort)0;
        _bitsSource.Value = (ushort)~currentBits;
        _textSource.Value = currentBool ? "Disabled" : "Enabled";
        PublishAll();
    }

    private void ExecuteStandby()
    {
        _floatSource.Value = 7.2;
        _textSource.Value = "Standby";
        _boolSource.Value = false;
        _bitsSource.Value = (ushort)0b0000_0011;
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
}
