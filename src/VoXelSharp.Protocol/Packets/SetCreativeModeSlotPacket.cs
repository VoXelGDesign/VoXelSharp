namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A creative-inventory item as sent by the client in
/// <see cref="SetCreativeModeSlotPacket"/> (26.3 <c>ItemStack.OPTIONAL_UNTRUSTED_STREAM_CODEC</c>).
/// Wire layout (verified from the 26.3 client):
/// - Count: VarInt. When &lt;= 0 the stack is empty and nothing more follows.
/// - Item ID: VarInt — the registry ID in <c>minecraft:item</c> (server reports assign
///   stone=1, grass_block=54, dirt=55, bedrock=87).
/// - Components: a <c>DataComponentPatch</c> whose values are length-prefixed
///   (VarInt length + bytes). This server only needs the item identity to place blocks, so
///   the component bytes are carried verbatim and round-tripped unchanged.
/// </summary>
public sealed record CreativeSlotItem(int Count, int ItemId, byte[] Components)
{
    /// <summary>An empty stack (Count &lt;= 0): the client holds nothing in this slot.</summary>
    public bool IsEmpty => Count <= 0;

    /// <summary>Reads a creative item, capturing the component patch verbatim.</summary>
    public static CreativeSlotItem Decode(ref PacketReader reader)
    {
        int count = reader.ReadVarInt();
        if (count <= 0)
        {
            return new CreativeSlotItem(count, 0, []);
        }

        int itemId = reader.ReadVarInt();
        byte[] components = reader.ReadBytes(reader.Remaining);
        return new CreativeSlotItem(count, itemId, components);
    }

    /// <summary>Writes the creative item back with its verbatim component bytes.</summary>
    public void Encode(PacketWriter writer)
    {
        writer.WriteVarInt(Count);
        if (Count <= 0)
        {
            return;
        }

        writer.WriteVarInt(ItemId);
        writer.WriteBytes(Components);
    }
}

/// <summary>
/// Set Creative Mode Slot (play/serverbound, packet ID 57, official name
/// "set_creative_mode_slot"). The creative-mode client reports what item it has placed into a
/// hotbar slot so the server knows what the player is holding when an item is used.
/// Field order per docs/data/packets.json and the 26.3 client
/// (ServerboundSetCreativeModeSlotPacket.STREAM_CODEC): Slot (Short), Item (ItemStack).
/// </summary>
public sealed class SetCreativeModeSlotPacket : IPacket
{
    public SetCreativeModeSlotPacket(short slot, CreativeSlotItem item)
    {
        Slot = slot;
        Item = item;
    }

    /// <summary>The hotbar slot (0..8 in creative mode).</summary>
    public short Slot { get; }

    /// <summary>The item now in that slot.</summary>
    public CreativeSlotItem Item { get; }
}

/// <summary>Codec for <see cref="SetCreativeModeSlotPacket"/>.</summary>
public sealed class SetCreativeModeSlotCodec : PacketCodec<SetCreativeModeSlotPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetCreativeModeSlotCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 57;

    /// <inheritdoc />
    protected override void EncodeBody(SetCreativeModeSlotPacket packet, PacketWriter writer)
    {
        writer.WriteShort(packet.Slot);
        packet.Item.Encode(writer);
    }

    /// <inheritdoc />
    protected override SetCreativeModeSlotPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadShort(), CreativeSlotItem.Decode(ref reader));
}
