using System;

namespace Amium.UdlClient;

public sealed class CanMessage
{
    public CanMessage(uint id, byte[] data)
    {
        Id = id;
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Date = DateTime.UtcNow;
    }

    public DateTime Date { get; }
    public uint Id { get; }
    public byte[] Data { get; }

    public override string ToString()
    {
        var text = $"{Id:X4}:";
        for (var index = 0; index < 8; index++)
        {
            text += index < Data.Length ? $" {Data[index]:X2}" : " --";
        }

        return text;
    }
}