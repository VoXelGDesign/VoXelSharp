using VoXelSharp.Protocol.Nbt;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// System Chat Message (play/clientbound, packet ID 124, official name "system_chat").
/// Sends the client a raw system message. Field order per docs/data/packets.json:
/// Content (Text Component, limited to 262144 bytes), Overlay (Boolean).
/// Text Component is NBT-encoded (docs/data/wire_types.md): a plain-text message uses a
/// TAG_String root; richer components use a TAG_Compound root.
/// Verified as protocol_id 124 against the Mojang data-generator report.
/// </summary>
public sealed class SystemChatPacket : IPacket
{
    /// <summary>Maximum Content size in bytes (docs/data/packets.json).</summary>
    public const int MaxContentLength = 262144;

    public SystemChatPacket(NbtNode content, bool overlay)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
        Overlay = overlay;
    }

    /// <summary>The message content as an NBT text component root.</summary>
    public NbtNode Content { get; }

    /// <summary>True renders as an actionbar message instead of chat.</summary>
    public bool Overlay { get; }
}

/// <summary>Codec for <see cref="SystemChatPacket"/>.</summary>
public sealed class SystemChatCodec : PacketCodec<SystemChatPacket>
{
    /// <summary>Singleton access.</summary>
    public static SystemChatCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 124;

    /// <inheritdoc />
    protected override void EncodeBody(SystemChatPacket packet, PacketWriter writer)
    {
        NbtWriter.WriteRootNode(writer, packet.Content);
        writer.WriteBoolean(packet.Overlay);
    }

    /// <inheritdoc />
    protected override SystemChatPacket DecodeBody(ref PacketReader reader)
    {
        NbtNode content = NbtReader.ReadRootNode(ref reader);
        bool overlay = reader.ReadBoolean();
        return new SystemChatPacket(content, overlay);
    }
}
