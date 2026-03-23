using Amium.Host;
using Amium.Items;

namespace DefinitionMain;

public class qPage : BookPage
{
    private readonly Item _temperatureSource = CreateDemoItem("Reference Temperature", "Runtime/Main/Reference", "degC", 22.5);
    private readonly Item _registerSource = CreateDemoItem("Reference Register", "Runtime/Main/Reference", "hex", (ushort)0x1248);

    private Item? _temperatureAttached;
    private Item? _registerAttached;
    public qPage() : base("Main")
    {
    }

    protected override void OnInitialize()
    {
        _temperatureAttached ??= Attach(_temperatureSource, "Reference/Temperature");
        _registerAttached ??= Attach(_registerSource, "Reference/Register");

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
        if (_temperatureAttached is not null)
        {
            PublishItem(_temperatureAttached);
        }

        if (_registerAttached is not null)
        {
            PublishItem(_registerAttached);
        }
    }

    private void PublishCommands()
    {
        PublishCommand("Boost", ExecuteBoostCommand, "Increase the demo values");
        PublishCommand("Reset", ExecuteResetCommand, "Reset the demo values");
    }

    private void ExecuteBoostCommand()
    {        _temperatureSource.Value = 28.4;
        _registerSource.Value = (ushort)0x2BCD;
        PublishAll();
    }

    private void ExecuteResetCommand()
    {        _temperatureSource.Value = 22.5;
        _registerSource.Value = (ushort)0x1248;
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
