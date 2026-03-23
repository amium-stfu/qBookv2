using Amium.Host;
using Amium.Items;

namespace DefinitionAllControls;

public class qPage : BookPage
{
    private readonly Item _floatSource = CreateDemoItem("Pressure Channel", "Runtime/AllControls/Float", "bar", 12.7);
    private readonly Item _textSource = CreateDemoItem("Machine State", "Runtime/AllControls/Text", string.Empty, "Ready");
    private readonly Item _boolSource = CreateDemoItem("Drive Enabled", "Runtime/AllControls/Bool", string.Empty, true);
    private readonly Item _bitsSource = CreateDemoItem("Output Mask", "Runtime/AllControls/Bits", string.Empty, (ushort)0b1010_0110);

    private Item? _floatAttached;
    private Item? _textAttached;
    private Item? _boolAttached;
    private Item? _bitsAttached;

    public qPage() : base("AllControls")
    {
    }

    protected override void OnInitialize()
    {
        _floatAttached ??= Attach(_floatSource, "Demo/Float");
        _textAttached ??= Attach(_textSource, "Demo/Text");
        _boolAttached ??= Attach(_boolSource, "Demo/Bool");
        _bitsAttached ??= Attach(_bitsSource, "Demo/Bits");

        PublishCommands();
        PublishAll();
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
            UiPublisher.Publish(_floatAttached);
        }

        if (_textAttached is not null)
        {
            UiPublisher.Publish(_textAttached);
        }

        if (_boolAttached is not null)
        {
            UiPublisher.Publish(_boolAttached);
        }

        if (_bitsAttached is not null)
        {
            UiPublisher.Publish(_bitsAttached);
        }
    }

    private void PublishCommands()
    {
        UiPublisher.Publish(AttachCommand("Pulse", ExecutePulse, "Set demo values to active"));
        UiPublisher.Publish(AttachCommand("Reset", ExecuteReset, "Reset demo values"));
        UiPublisher.Publish(AttachCommand("Toggle", ExecuteToggle, "Toggle bool and bits"));
        UiPublisher.Publish(AttachCommand("Standby", ExecuteStandby, "Set demo values to standby"));
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
        _floatSource.Value = 12.7;
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
