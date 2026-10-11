using VoXelSharp.Protocol.Nbt;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A single tag in an <see cref="UpdateTagsPacket"/>.
/// Wire format per docs/data/wire_types.md: Tag Name (Identifier), Entries (Prefixed Array of VarInt).
/// </summary>
public sealed record RegistryTag(string Name, int[] EntryIds)
{
    /// <summary>Creates a tag with no entries (the tag exists so references resolve).</summary>
    public static RegistryTag Empty(string name) => new(name, []);

    /// <summary>Creates a tag with the given entry IDs.</summary>
    public static RegistryTag With(string name, params int[] entryIds) => new(name, entryIds);
}

/// <summary>
/// A registry with its tags in an <see cref="UpdateTagsPacket"/>.
/// Wire format per docs/data/packets.json: Registry (Identifier), Tags (Prefixed Array of Tag).
/// </summary>
public sealed record TaggedRegistry(string Registry, RegistryTag[] Tags)
{
    /// <summary>
    /// Creates a tagged registry whose tags all have empty entry lists. Used for tags that must
    /// exist so references resolve but whose entries come from the client's pack.
    /// </summary>
    public static TaggedRegistry WithEmptyTags(string registry, params string[] tagNames)
    {
        var tags = new RegistryTag[tagNames.Length];
        for (int i = 0; i < tagNames.Length; i++)
        {
            tags[i] = RegistryTag.Empty(tagNames[i]);
        }

        return new TaggedRegistry(registry, tags);
    }
}

/// <summary>
/// Update Tags (configuration/clientbound, packet ID 14, official name "update_tags").
/// Updates the client's registry tag definitions. Each tagged registry lists tags containing
/// numeric entry IDs from that registry.
/// Field order per docs/data/packets.json:
/// Tagged Registries (Prefixed Array of {Registry: Identifier, Tags: Prefixed Array of Tag}).
/// Each Tag is: Tag Name (Identifier), Entries (Prefixed Array of VarInt) (docs/data/wire_types.md).
/// </summary>
public sealed class UpdateTagsPacket : IPacket
{
    /// <summary>Maximum length for a registry or tag name identifier.</summary>
    public const int MaxIdentifierLength = 32767;

    public UpdateTagsPacket(TaggedRegistry[] taggedRegistries)
    {
        ArgumentNullException.ThrowIfNull(taggedRegistries);
        TaggedRegistries = taggedRegistries;
    }

    /// <summary>The list of tagged registries.</summary>
    public TaggedRegistry[] TaggedRegistries { get; }
}

/// <summary>Codec for <see cref="UpdateTagsPacket"/>.</summary>
public sealed class UpdateTagsCodec : PacketCodec<UpdateTagsPacket>
{
    /// <summary>Singleton access.</summary>
    public static UpdateTagsCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 14;

    /// <inheritdoc />
    protected override void EncodeBody(UpdateTagsPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.TaggedRegistries.Length);
        foreach (TaggedRegistry tr in packet.TaggedRegistries)
        {
            writer.WriteString(tr.Registry, UpdateTagsPacket.MaxIdentifierLength);

            writer.WriteVarInt(tr.Tags.Length);
            foreach (RegistryTag tag in tr.Tags)
            {
                writer.WriteString(tag.Name, UpdateTagsPacket.MaxIdentifierLength);

                writer.WriteVarInt(tag.EntryIds.Length);
                foreach (int entryId in tag.EntryIds)
                {
                    writer.WriteVarInt(entryId);
                }
            }
        }
    }

    /// <inheritdoc />
    protected override UpdateTagsPacket DecodeBody(ref PacketReader reader)
    {
        int registryCount = reader.ReadVarInt();
        if (registryCount < 0)
        {
            throw new InvalidDataException($"Negative tagged-registry count ({registryCount}).");
        }

        var registries = new TaggedRegistry[registryCount];
        for (int r = 0; r < registryCount; r++)
        {
            string registryId = reader.ReadString(UpdateTagsPacket.MaxIdentifierLength);

            int tagCount = reader.ReadVarInt();
            if (tagCount < 0)
            {
                throw new InvalidDataException($"Negative tag count ({tagCount}).");
            }

            var tags = new RegistryTag[tagCount];
            for (int t = 0; t < tagCount; t++)
            {
                string tagName = reader.ReadString(UpdateTagsPacket.MaxIdentifierLength);

                int entryCount = reader.ReadVarInt();
                if (entryCount < 0)
                {
                    throw new InvalidDataException($"Negative tag entry count ({entryCount}).");
                }

                var entryIds = new int[entryCount];
                for (int e = 0; e < entryCount; e++)
                {
                    entryIds[e] = reader.ReadVarInt();
                }

                tags[t] = new RegistryTag(tagName, entryIds);
            }

            registries[r] = new TaggedRegistry(registryId, tags);
        }

        return new UpdateTagsPacket(registries);
    }
}
