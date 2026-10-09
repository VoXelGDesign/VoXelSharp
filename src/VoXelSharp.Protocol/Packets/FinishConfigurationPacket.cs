namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Finish Configuration (configuration/clientbound, packet ID 3, official name "finish_configuration").
/// Tells the client that the configuration process is finished; the client validates and responds
/// with Acknowledge Finish Configuration. The connection state then transitions to Play.
/// No fields (docs/data/packets.json).
/// </summary>
public sealed class FinishConfigurationPacket : IPacket
{
}

/// <summary>Codec for <see cref="FinishConfigurationPacket"/>.</summary>
public sealed class FinishConfigurationCodec : PacketCodec<FinishConfigurationPacket>
{
    /// <summary>Singleton access.</summary>
    public static FinishConfigurationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 3;

    /// <inheritdoc />
    protected override void EncodeBody(FinishConfigurationPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override FinishConfigurationPacket DecodeBody(ref PacketReader reader) => new();
}
