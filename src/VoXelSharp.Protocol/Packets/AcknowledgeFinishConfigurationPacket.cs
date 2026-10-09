namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Acknowledge Finish Configuration (configuration/serverbound, packet ID 3, official name "finish_configuration").
/// The client sends this after validating the registry and tag data received from the server
/// during Configuration. The server should then transition to Play.
/// No fields (docs/data/packets.json).
/// </summary>
public sealed class AcknowledgeFinishConfigurationPacket : IPacket
{
}

/// <summary>Codec for <see cref="AcknowledgeFinishConfigurationPacket"/>.</summary>
public sealed class AcknowledgeFinishConfigurationCodec : PacketCodec<AcknowledgeFinishConfigurationPacket>
{
    /// <summary>Singleton access.</summary>
    public static AcknowledgeFinishConfigurationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 3;

    /// <inheritdoc />
    protected override void EncodeBody(AcknowledgeFinishConfigurationPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override AcknowledgeFinishConfigurationPacket DecodeBody(ref PacketReader reader) => new();
}
