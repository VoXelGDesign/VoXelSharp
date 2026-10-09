using VoXelSharp.Protocol.Nbt;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A single registry data entry in <see cref="RegistryDataPacket"/>.
/// </summary>
public sealed record RegistryDataEntry(string EntryId, NbtCompound? Data);

/// <summary>
/// Registry Data (configuration/clientbound, packet ID 7, official name "registry_data").
/// Informs the client of the contents of a synchronized registry.
/// Field order per docs/data/packets.json:
/// Registry ID (Identifier), Entries (Prefixed Array of {Entry ID: Identifier, Data: Prefixed Optional NBT}).
/// </summary>
public sealed class RegistryDataPacket : IPacket
{
    /// <summary>Maximum length for a registry ID or entry ID string in UTF-16 code units.</summary>
    public const int MaxIdentifierLength = 32767;

    public RegistryDataPacket(string registryId, RegistryDataEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(registryId);
        ArgumentNullException.ThrowIfNull(entries);
        RegistryId = registryId;
        Entries = entries;
    }

    /// <summary>Registry name, e.g. "minecraft:dimension_type".</summary>
    public string RegistryId { get; }

    /// <summary>Registry entries.</summary>
    public RegistryDataEntry[] Entries { get; }
}

/// <summary>Codec for <see cref="RegistryDataPacket"/>.</summary>
public sealed class RegistryDataCodec : PacketCodec<RegistryDataPacket>
{
    /// <summary>Singleton access.</summary>
    public static RegistryDataCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 7;

    /// <inheritdoc />
    protected override void EncodeBody(RegistryDataPacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.RegistryId, RegistryDataPacket.MaxIdentifierLength);

        writer.WriteVarInt(packet.Entries.Length);
        foreach (RegistryDataEntry entry in packet.Entries)
        {
            writer.WriteString(entry.EntryId, RegistryDataPacket.MaxIdentifierLength);

            if (entry.Data is not null)
            {
                writer.WriteBoolean(true);
                NbtWriter.WriteRoot(writer, entry.Data);
            }
            else
            {
                writer.WriteBoolean(false);
            }
        }
    }

    /// <inheritdoc />
    protected override RegistryDataPacket DecodeBody(ref PacketReader reader)
    {
        string registryId = reader.ReadString(RegistryDataPacket.MaxIdentifierLength);

        int entryCount = reader.ReadVarInt();
        if (entryCount < 0)
        {
            throw new InvalidDataException($"Negative registry entry count ({entryCount}).");
        }

        var entries = new RegistryDataEntry[entryCount];
        for (int i = 0; i < entryCount; i++)
        {
            string entryId = reader.ReadString(RegistryDataPacket.MaxIdentifierLength);

            bool hasData = reader.ReadBoolean();
            NbtCompound? data = null;
            if (hasData)
            {
                data = NbtReader.ReadRoot(ref reader);
            }

            entries[i] = new RegistryDataEntry(entryId, data);
        }

        return new RegistryDataPacket(registryId, entries);
    }
}
