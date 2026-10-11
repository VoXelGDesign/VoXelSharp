namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Set Player Inventory (play/clientbound, packet ID 110, official name "set_player_inventory").
/// Updates a single inventory slot. Field order verified from the 26.3 client
/// (ClientboundSetPlayerInventoryPacket.STREAM_CODEC): Slot (VarInt),
/// Contents (ItemStack.OPTIONAL_STREAM_CODEC = Count VarInt, Item VarInt, component patch).
/// The client applies it to the inventory slot so the item is visible in the UI/hotbar.
/// </summary>
public sealed class SetPlayerInventoryPacket : IPacket
{
    public SetPlayerInventoryPacket(int slot, int count, int itemId)
    {
        Slot = slot;
        Count = count;
        ItemId = itemId;
    }

    /// <summary>Inventory slot index (0..12 hotbar, 13+ inventory).</summary>
    public int Slot { get; }

    /// <summary>The stack count (0 = empty slot).</summary>
    public int Count { get; }

    /// <summary>Item registry ID of the contents (ignored when Count is 0).</summary>
    public int ItemId { get; }
}

/// <summary>Codec for <see cref="SetPlayerInventoryPacket"/>.</summary>
public sealed class SetPlayerInventoryCodec : PacketCodec<SetPlayerInventoryPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetPlayerInventoryCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 110;

    /// <inheritdoc />
    protected override void EncodeBody(SetPlayerInventoryPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.Slot);
        writer.WriteVarInt(packet.Count);
        if (packet.Count > 0)
        {
            writer.WriteVarInt(packet.ItemId);
            // ItemStack.OPTIONAL_STREAM_CODEC: empty component patch (0, 0).
            writer.WriteVarInt(0);
            writer.WriteVarInt(0);
        }
    }

    /// <inheritdoc />
    protected override SetPlayerInventoryPacket DecodeBody(ref PacketReader reader)
    {
        int slot = reader.ReadVarInt();
        int count = reader.ReadVarInt();
        int itemId = 0;
        if (count > 0)
        {
            itemId = reader.ReadVarInt();
            int positiveCount = reader.ReadVarInt();
            int negativeCount = reader.ReadVarInt();
            if (positiveCount != 0 || negativeCount != 0)
            {
                throw new InvalidDataException("Inventory slot with components is not supported in this milestone.");
            }
        }

        return new SetPlayerInventoryPacket(slot, count, itemId);
    }
}
