namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Keep Alive (play/clientbound, packet ID 45, official name "keep_alive").
/// The server sends this periodically; the client must echo the Keep Alive ID in the
/// serverbound Keep Alive packet. Field order per docs/data/packets.json: Keep Alive ID (Long).
/// Verified as protocol_id 45 against the Mojang data-generator report.
/// </summary>
public sealed class PlayKeepAlivePacket : IPacket
{
    public PlayKeepAlivePacket(long keepAliveId)
    {
        KeepAliveId = keepAliveId;
    }

    /// <summary>Per-connection identifier echoed back by the client.</summary>
    public long KeepAliveId { get; }
}

/// <summary>Codec for the clientbound Play Keep Alive packet.</summary>
public sealed class PlayKeepAliveCodec : PacketCodec<PlayKeepAlivePacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayKeepAliveCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 45;

    /// <inheritdoc />
    protected override void EncodeBody(PlayKeepAlivePacket packet, PacketWriter writer)
        => writer.WriteLong(packet.KeepAliveId);

    /// <inheritdoc />
    protected override PlayKeepAlivePacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadLong());
}

/// <summary>
/// Keep Alive (play/serverbound, packet ID 28, official name "keep_alive").
/// Client response to the clientbound Keep Alive. Same wire shape; the session should verify
/// the echo matches the server's last Keep Alive ID before accepting it.
/// Field order per docs/data/packets.json: Keep Alive ID (Long).
/// Verified as protocol_id 28 against the Mojang data-generator report.
/// </summary>
public sealed class ServerPlayKeepAliveCodec : PacketCodec<PlayKeepAlivePacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerPlayKeepAliveCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 28;

    /// <inheritdoc />
    protected override void EncodeBody(PlayKeepAlivePacket packet, PacketWriter writer)
        => writer.WriteLong(packet.KeepAliveId);

    /// <inheritdoc />
    protected override PlayKeepAlivePacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadLong());
}
