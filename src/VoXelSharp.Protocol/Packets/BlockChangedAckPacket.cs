namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Block Changed Ack (play/clientbound, packet ID 4, official name "block_changed_ack").
/// Sent by the server after processing a serverbound player_action to acknowledge its
/// sequence number, so the client stops its block-break crack animation. Carries the echoed
/// Sequence (VarInt). Field order verified from the 26.3 client
/// (ClientboundBlockChangedAckPacket.STREAM_CODEC).
/// </summary>
public sealed class BlockChangedAckPacket : IPacket
{
    public BlockChangedAckPacket(int sequence)
    {
        Sequence = sequence;
    }

    /// <summary>The acknowledged player_action sequence number.</summary>
    public int Sequence { get; }
}

/// <summary>Codec for <see cref="BlockChangedAckPacket"/>.</summary>
public sealed class BlockChangedAckCodec : PacketCodec<BlockChangedAckPacket>
{
    /// <summary>Singleton access.</summary>
    public static BlockChangedAckCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 4;

    /// <inheritdoc />
    protected override void EncodeBody(BlockChangedAckPacket packet, PacketWriter writer)
        => writer.WriteVarInt(packet.Sequence);

    /// <inheritdoc />
    protected override BlockChangedAckPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt());
}
