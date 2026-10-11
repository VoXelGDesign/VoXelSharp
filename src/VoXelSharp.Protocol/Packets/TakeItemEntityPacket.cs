namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Take Item Entity (play/clientbound, packet ID 127, official name "take_item_entity").
/// Tells the client an item entity was picked up by a player: it animates the remaining amount
/// toward the player and removes the entity when the stack empties.
/// Field order verified from the 26.3 client (ClientboundTakeItemEntityPacket):
/// Item Entity ID (VarInt), Player ID (VarInt), Amount (VarInt).
/// </summary>
public sealed class TakeItemEntityPacket : IPacket
{
    public TakeItemEntityPacket(int itemEntityId, int playerId, int amount)
    {
        ItemEntityId = itemEntityId;
        PlayerId = playerId;
        Amount = amount;
    }

    /// <summary>The item entity being picked up.</summary>
    public int ItemEntityId { get; }

    /// <summary>The player picking it up.</summary>
    public int PlayerId { get; }

    /// <summary>The amount to take (removes the entity when its stack empties).</summary>
    public int Amount { get; }
}

/// <summary>Codec for <see cref="TakeItemEntityPacket"/>.</summary>
public sealed class TakeItemEntityCodec : PacketCodec<TakeItemEntityPacket>
{
    /// <summary>Singleton access.</summary>
    public static TakeItemEntityCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 127;

    /// <inheritdoc />
    protected override void EncodeBody(TakeItemEntityPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.ItemEntityId);
        writer.WriteVarInt(packet.PlayerId);
        writer.WriteVarInt(packet.Amount);
    }

    /// <inheritdoc />
    protected override TakeItemEntityPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt(), reader.ReadVarInt(), reader.ReadVarInt());
}
