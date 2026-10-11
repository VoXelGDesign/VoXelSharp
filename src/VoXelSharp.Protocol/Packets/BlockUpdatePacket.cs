namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Block Update (play/clientbound, packet ID 8, official name "block_update").
/// Tells the client a single block changed. Sent when blocks are broken/placed so the
/// client does not wait for a full chunk re-send.
/// Field order verified from the 26.3 client (ClientboundBlockUpdatePacket.STREAM_CODEC):
/// Location (Position/BlockPos), Block ID (VarInt, the global block state ID from the
/// <c>minecraft:block</c> registry; matches docs/data/packets.json).
/// </summary>
public sealed class BlockUpdatePacket : IPacket
{
    public BlockUpdatePacket(BlockPosition location, int blockStateId)
    {
        Location = location;
        BlockStateId = blockStateId;
    }

    /// <summary>The block position that changed.</summary>
    public BlockPosition Location { get; }

    /// <summary>Global block state ID (docs/data/blocks.json).</summary>
    public int BlockStateId { get; }
}

/// <summary>Codec for <see cref="BlockUpdatePacket"/>.</summary>
public sealed class BlockUpdateCodec : PacketCodec<BlockUpdatePacket>
{
    /// <summary>Singleton access.</summary>
    public static BlockUpdateCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 8;

    /// <inheritdoc />
    protected override void EncodeBody(BlockUpdatePacket packet, PacketWriter writer)
    {
        writer.WritePosition(packet.Location);
        writer.WriteVarInt(packet.BlockStateId);
    }

    /// <inheritdoc />
    protected override BlockUpdatePacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadPosition(), reader.ReadVarInt());
}
