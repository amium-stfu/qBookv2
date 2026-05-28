using System;
using ItemModel = Amium.Items.Item;
using Amium.Items;
using HornetStudio.Editor.Models;

namespace HornetStudio.Host
{
    internal sealed class UdlModule : ItemModel
    {
        private const string ReadItemName = "read";
        private const string SetItemName = "set";
        private const string OutItemName = "out";
        private const string StateItemName = "state";
        private const string AlertItemName = "alert";
        private const string FloatTypeName = "float";
        private const string IntTypeName = "int";

        public UdlModule(string name, string? path = null)
            : base(name, path: path)
        {
            Properties["kind"].Value = "UdlModule";
            Properties["text"].Value = name;
            Properties["unit"].Value = string.Empty;

            AddChannel(ReadItemName, FloatTypeName, hasWriteChannel: true);
            AddChannel(SetItemName, FloatTypeName, hasWriteChannel: true);
            AddChannel(OutItemName, FloatTypeName, hasWriteChannel: true);
            AddChannel(StateItemName, IntTypeName, hasWriteChannel: true);
            AddChannel(AlertItemName, IntTypeName);
        }

        public ItemModel Read => this[ReadItemName];
        public ItemModel Set => this[SetItemName];
        public ItemModel Out => this[OutItemName];
        public ItemModel State => this[StateItemName];
        public ItemModel Alert => this[AlertItemName];

        public void EnsureWriteMetadata()
        {
            EnsureChannelType(Read, FloatTypeName);
            EnsureChannelType(Set, FloatTypeName);
            EnsureChannelType(Out, FloatTypeName);
            EnsureChannelType(State, IntTypeName);
            EnsureChannelType(Alert, IntTypeName);
            EnsureWriteChannel(Read);
            EnsureWriteChannel(Set);
            EnsureWriteChannel(Out);
            EnsureWriteChannel(State);
            EnsureNoWriteChannel(Alert);
            RemoveLegacyCommand();
        }

        private void AddChannel(string name, string targetType, bool hasWriteChannel = false)
        {
            var channel = new ItemModel(
                name,
                path: Path,
                hasWriteChannel: hasWriteChannel);
            EnsureChannelType(channel, targetType);
            this[name] = channel;
        }

        private static void EnsureChannelType(ItemModel channel, string targetType)
        {
            channel.Properties["type"].Value = targetType;
        }

        private static void EnsureWriteChannel(ItemModel channel)
        {
            if (!channel.Properties.Has("write"))
            {
                channel.Properties["write"].Value = channel.Properties.Has("read")
                    ? channel.Properties["read"].Value
                    : null!;
            }
        }

        private static void EnsureNoWriteChannel(ItemModel channel)
        {
            channel.Properties.Remove("write");
        }

        private void RemoveLegacyCommand()
        {
            if (Has("Command"))
            {
                Remove("Command");
            }
        }
    }
}
