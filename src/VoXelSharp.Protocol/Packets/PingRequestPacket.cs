namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Ping Request (status/serverbound, packet ID 1, official name "ping_request").
/// Field order per docs/data/packets.json: Timestamp (Long).
/// </summary>
public sealed class PingRequestPacket : IPacket
{
    public PingRequestPacket(long timestamp)
    {
        Timestamp = timestamp;
    }

    /// <summary>Arbitrary timestamp; vanilla clients send the departure time in milliseconds.</summary>
    public long Timestamp { get; }
}

/// <summary>Codec for <see cref="PingRequestPacket"/>.</summary>
public sealed class PingRequestCodec : PacketCodec<PingRequestPacket>
{
    /// <summary>Singleton access.</summary>
    public static PingRequestCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Status;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 1;

    /// <inheritdoc />
    protected override void EncodeBody(PingRequestPacket packet, PacketWriter writer)
    {
        writer.WriteLong(packet.Timestamp);
    }

    /// <inheritdoc />
    protected override PingRequestPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadLong());
}
