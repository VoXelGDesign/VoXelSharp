namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Status Request (status/serverbound, packet ID 0, official name "status_request").
/// No fields (docs/data/packets.json).
/// </summary>
public sealed class StatusRequestPacket : IPacket
{
}

/// <summary>Codec for <see cref="StatusRequestPacket"/>.</summary>
public sealed class StatusRequestCodec : PacketCodec<StatusRequestPacket>
{
    /// <summary>Singleton access.</summary>
    public static StatusRequestCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Status;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(StatusRequestPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override StatusRequestPacket DecodeBody(ref PacketReader reader) => new();
}
