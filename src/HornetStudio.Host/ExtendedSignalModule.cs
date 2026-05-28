using ItemModel = Amium.Items.Item;
using Amium.Items;

namespace HornetStudio.Host;

public sealed class ExtendedSignalModule : ItemModel
{
    private const string RawItemName = "raw";
    private const string ReadItemName = "read";
    private const string SetItemName = "set";
    private const string OutItemName = "out";
    private const string StateItemName = "state";
    private const string AlertItemName = "alert";
    private const string CommandItemName = "command";
    private const string ConfigItemName = "config";
    private const string BoolTypeName = "bool";
    private const string FloatTypeName = "float";
    private const string ObjectTypeName = "object";
    private const string StringTypeName = "string";

    public ExtendedSignalModule(string name, string? path = null)
        : base(name, path: path)
    {
        Properties["kind"].Value = "ExtendedSignalModule";
        Properties["text"].Value = name;
        Properties["type"].Value = FloatTypeName;
        Properties["unit"].Value = string.Empty;

        AddChannel(RawItemName, FloatTypeName);
        AddChannel(ReadItemName, FloatTypeName, hasWriteChannel: true);
        AddChannel(SetItemName, FloatTypeName, hasWriteChannel: true);
        AddChannel(OutItemName, FloatTypeName, hasWriteChannel: true);
        AddChannel(StateItemName, StringTypeName, hasWriteChannel: true);
        AddChannel(AlertItemName, StringTypeName);
        AddChannel(CommandItemName, BoolTypeName, hasWriteChannel: true);
        AddItemWithType(ConfigItemName, ObjectTypeName);
    }

    public ItemModel Raw => this[RawItemName];

    public ItemModel Read => this[ReadItemName];

    public ItemModel Set => this[SetItemName];

    public ItemModel Out => this[OutItemName];

    public ItemModel State => this[StateItemName];

    public ItemModel Alert => this[AlertItemName];

    public ItemModel Command => this[CommandItemName];

    public ItemModel Config => this[ConfigItemName];

    private void AddChannel(string name, string targetType, bool hasWriteChannel = false)
    {
        var channel = new ItemModel(
            name,
            path: Path,
            hasWriteChannel: hasWriteChannel);
        channel.Properties["type"].Value = targetType;
        this[name] = channel;
    }

    private void AddItemWithType(string name, string targetType)
    {
        var item = new ItemModel(name, path: Path);
        item.Properties["type"].Value = targetType;
        this[name] = item;
    }
}
