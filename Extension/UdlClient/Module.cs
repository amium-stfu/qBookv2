using Amium.Items;

namespace UdlClient;

public sealed class Module : Item
{
    public Module(string name, string? path = null)
        : base(name, path: path)
    {
        Params["Kind"].Value = "UdlModule";
        Params["Text"].Value = name;

        AddItem($"{name}.Set");
        AddItem($"{name}.Out");
        AddItem($"{name}.State");
        AddItem($"{name}.Unit");
        AddItem($"{name}.alert");
        AddItem($"{name}.Command");
        AddItem($"{name}.CommandSet");
    }

    public Item Set => this[$"{Name}.Set"];
    public Item Out => this[$"{Name}.Out"];
    public Item State => this[$"{Name}.State"];
    public Item Unit => this[$"{Name}.Unit"];
    public Item Alert => this[$"{Name}.alert"];
    public Item Command => this[$"{Name}.Command"];
    public Item CommandSet => this[$"{Name}.CommandSet"];

    public bool WriteBack { get; set; }
}