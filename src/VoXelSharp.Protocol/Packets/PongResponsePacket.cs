namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Pong Response (status/clientbound, packet ID 1, official name "pong_response").
/// Field order per docs/data/packets.json: Timestamp (Long).
/// </summary>
public sealed class PongResponsePacket : IPacket
{
    public PongResponsePacket(long timestamp)
    {
        Timestamp = timestamp;
    }

    /// <summary>Echoes the timestamp from the corresponding Ping Request.</summary>
    public long Timestamp { get; }
}

/// <summary>Codec for <see cref="PongResponsePacket"/>.</summary>
public sealed class PongResponseCodec : PacketCodec<PongResponsePacket>
{
    /// <summary>Singleton access.</summary>
    public static PongResponseCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Status;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 1;

    /// <inheritdoc />
    protected override void EncodeBody(PongResponsePacket packet, PacketWriter writer)
    {
        writer.WriteLong(packet.Timestamp);
    }

    /// <inheritdoc />
    protected override PongResponsePacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadLong());
}
