namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A single synched entity-data value in <see cref="SetEntityDataPacket"/>. Wire form (26.3
/// SynchedEntityData.DataValue): ID (Byte — the entity's field id, e.g. 0 for an item entity's
/// item slot), Type (VarInt — the serializer id; ITEM_STACK = 7), then the serializer payload.
/// The ITEM_STACK payload is an item stack in its optional stream form: Count (VarInt; 0 = empty),
/// then Item ID (VarInt, minecraft:item registry) and the component patch.
/// </summary>
public sealed record EntityDataItemValue(int Id, int SerializerId, int Count, int ItemId);

/// <summary>
/// Set Entity Data (play/clientbound, packet ID 101, official name "set_entity_data"). Sends
/// synched entity data (e.g. the item an ItemEntity is carrying) for one entity.
/// Wire form verified from the 26.3 client (ClientboundSetEntityDataPacket): Entity ID (VarInt),
/// then Data Values until EOF_MARKER 0xFF. Each value: Type (VarInt serializer id) + payload.
/// </summary>
public sealed class SetEntityDataPacket : IPacket
{
    /// <summary>Terminator byte ending the packed data values.</summary>
    public const int EofMarker = 255;

    /// <summary>Serializer id of ITEM_STACK (EntityDataSerializers registration order).</summary>
    public const int ItemStackSerializerId = 7;

    /// <summary>
    /// Synced-data field id of ItemEntity's item slot on the 26.3 client. Ids are allocated
    /// per class hierarchy: Entity defines 8 base fields (ids 0-7), so ItemEntity.DATA_ITEM is id 8.
    /// Verified from the 26.3 client (Entity/ItemEntity defineSynchedData + ClassTreeIdRegistry).
    /// </summary>
    public const int ItemEntityItemFieldId = 8;

    public SetEntityDataPacket(int entityId, EntityDataItemValue[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        EntityId = entityId;
        Values = values;
    }

    /// <summary>The entity whose data is being set.</summary>
    public int EntityId { get; }

    /// <summary>The packed data values (each of the same entity).</summary>
    public EntityDataItemValue[] Values { get; }
}

/// <summary>Codec for <see cref="SetEntityDataPacket"/>.</summary>
public sealed class SetEntityDataCodec : PacketCodec<SetEntityDataPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetEntityDataCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 101;

    /// <inheritdoc />
    protected override void EncodeBody(SetEntityDataPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.EntityId);
        foreach (var value in packet.Values)
        {
            writer.WriteByte((byte)value.Id);
            writer.WriteVarInt(value.SerializerId);
            writer.WriteVarInt(value.Count);           // optional stack: count; 0 = empty
            if (value.Count > 0)
            {
                writer.WriteVarInt(value.ItemId);      // item registry id
                // ItemStack.OPTIONAL_STREAM_CODEC follows the id with a DataComponentPatch;
                // an empty patch is two VarInts (positive count 0, negative count 0). We never
                // send components for dropped blocks, so always write the empty patch.
                writer.WriteVarInt(0);
                writer.WriteVarInt(0);
            }
        }

        writer.WriteByte(SetEntityDataPacket.EofMarker);
    }

    /// <inheritdoc />
    protected override SetEntityDataPacket DecodeBody(ref PacketReader reader)
    {
        int entityId = reader.ReadVarInt();
        var values = new List<EntityDataItemValue>();
        while (true)
        {
            int id = reader.ReadByte();
            if (id == SetEntityDataPacket.EofMarker)
            {
                break;
            }

            int serializerId = reader.ReadVarInt();
            if (serializerId == SetEntityDataPacket.ItemStackSerializerId)
            {
                int count = reader.ReadVarInt();
                int itemId = 0;
                if (count > 0)
                {
                    itemId = reader.ReadVarInt();
                    // Consume the DataComponentPatch (empty => two zeros). Non-empty patches are
                    // not representable in this milestone, so anything else is a protocol error.
                    int positiveCount = reader.ReadVarInt();
                    int negativeCount = reader.ReadVarInt();
                    if (positiveCount != 0 || negativeCount != 0)
                    {
                        throw new InvalidDataException("Item entity data with components is not supported in this milestone.");
                    }
                }

                values.Add(new EntityDataItemValue(id, serializerId, count, itemId));
            }
            else
            {
                // Unknown serializer: without a full codec table this milestone cannot skip
                // arbitrary payloads, so reject rather than misparse.
                throw new InvalidDataException($"Unhandled entity data serializer id ({serializerId}).");
            }
        }

        return new SetEntityDataPacket(entityId, [.. values]);
    }
}
