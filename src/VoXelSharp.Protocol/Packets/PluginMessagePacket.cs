namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Plugin Message (custom_payload). One wire body is shared by the Configuration and Play
/// variants in both directions: Channel (Identifier) followed by Data (Varies - the raw
/// remaining bytes; there is NO data length prefix at the packet level). Per-channel formats
/// define their own prefixes (e.g. the standard <c>minecraft:brand</c> channel carries a
/// String (32767)). Facts from docs/data/_raw/_packets.raw.wikitext.txt (wiki, 2026-10-09)
/// and docs/data/packets.json:
///   Configuration serverbound id 2, Configuration clientbound id 1,
///   Play serverbound id 22, Play clientbound id 24.
/// Verified against the Mojang data-generator report.
/// </summary>
public sealed class PluginMessagePacket : IPacket
{
    /// <summary>Maximum channel (Identifier) length in UTF-16 code units (protocol String limit).</summary>
    public const int MaxChannelLength = 32767;

    /// <summary>
    /// Defensive decode cap for Data, derived from the overall frame limit (docs/PROTOCOL.md
    /// "Security Limits"): the wire places no per-packet data limit here, so the frame bound
    /// is the authority. Vanilla clients enforce 1048576 bytes on unrecognized channels.
    /// </summary>
    public const int MaxDataLength = 2_097_151;

    /// <summary>The <c>minecraft:brand</c> channel announces the implementation after login.</summary>
    public const string BrandChannel = "minecraft:brand";

    public PluginMessagePacket(string channel, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(data);

        if (channel.Length > MaxChannelLength)
        {
            throw new ArgumentException(
                $"Plugin channel of {channel.Length} UTF-16 code units exceeds the limit of {MaxChannelLength}.",
                nameof(channel));
        }

        Channel = channel;
        Data = data;
    }

    public string Channel { get; }

    /// <summary>The channel payload exactly as received; format depends on the channel.</summary>
    public byte[] Data { get; }
}

/// <summary>Shared Plugin Message body encoding; identical in both states and directions.</summary>
internal static class PluginMessageIO
{
    public static void Encode(PluginMessagePacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.Channel, PluginMessagePacket.MaxChannelLength);
        writer.WriteBytes(packet.Data);
    }

    public static PluginMessagePacket Decode(ref PacketReader reader)
    {
        string channel = reader.ReadString(PluginMessagePacket.MaxChannelLength);
        if (reader.Remaining > PluginMessagePacket.MaxDataLength)
        {
            throw new InvalidDataException(
                $"Plugin Message data of {reader.Remaining} bytes exceeds the {PluginMessagePacket.MaxDataLength}-byte limit.");
        }

        byte[] data = reader.ReadBytes(reader.Remaining);
        return new PluginMessagePacket(channel, data);
    }
}

/// <summary>Codec for the Configuration serverbound Plugin Message (custom_payload, id 2).</summary>
public sealed class ServerConfigurationPluginMessageCodec : PacketCodec<PluginMessagePacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerConfigurationPluginMessageCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 2;

    /// <inheritdoc />
    protected override void EncodeBody(PluginMessagePacket packet, PacketWriter writer) => PluginMessageIO.Encode(packet, writer);

    /// <inheritdoc />
    protected override PluginMessagePacket DecodeBody(ref PacketReader reader) => PluginMessageIO.Decode(ref reader);
}

/// <summary>Codec for the Play serverbound Plugin Message (custom_payload, id 22).</summary>
public sealed class PlayServerboundPluginMessageCodec : PacketCodec<PluginMessagePacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayServerboundPluginMessageCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 22;

    /// <inheritdoc />
    protected override void EncodeBody(PluginMessagePacket packet, PacketWriter writer) => PluginMessageIO.Encode(packet, writer);

    /// <inheritdoc />
    protected override PluginMessagePacket DecodeBody(ref PacketReader reader) => PluginMessageIO.Decode(ref reader);
}

/// <summary>Codec for the Play clientbound Plugin Message (custom_payload, id 24).</summary>
public sealed class PlayClientboundPluginMessageCodec : PacketCodec<PluginMessagePacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayClientboundPluginMessageCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 24;

    /// <inheritdoc />
    protected override void EncodeBody(PluginMessagePacket packet, PacketWriter writer) => PluginMessageIO.Encode(packet, writer);

    /// <inheritdoc />
    protected override PluginMessagePacket DecodeBody(ref PacketReader reader) => PluginMessageIO.Decode(ref reader);
}
