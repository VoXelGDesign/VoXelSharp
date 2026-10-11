using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class EntitySpawnPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void AddEntityRoundTrips()
    {
        var original = new AddEntityPacket(33, 1, 2, AddEntityPacket.EntityTypeItem, 10.5, 64.0, -5.25, 0.0, 0.0, 0.25, 0f, 90f, 0f, 0);
        var decoded = AddEntityCodec.Instance.DecodeAs<AddEntityPacket>(
            _play, PacketDirection.Clientbound, AddEntityCodec.Instance.Encode(original));

        Assert.Equal(33, decoded.EntityId);
        Assert.Equal(1, decoded.UuidMsb);
        Assert.Equal(2, decoded.UuidLsb);
        Assert.Equal(AddEntityPacket.EntityTypeItem, decoded.EntityTypeId);
        Assert.Equal(10.5, decoded.X);
        Assert.Equal(64.0, decoded.Y);
        Assert.Equal(-5.25, decoded.Z);
        Assert.Equal(0.0, decoded.MovementX);
        Assert.Equal(90f, decoded.YRot);
        Assert.Equal(0, decoded.Data);
    }

    [Fact]
    public void AddEntityKnownWireForm()
    {
        // Packet ID 1, entity id 1, uuid (0,0), item type 72, pos (0,0.5,0), zero movement,
        // zero rotations, data 0.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteVarInt(72);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(0.5);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteByte(0); // zero movement (LpVec3)
        writer.Writer.WriteByte(0);
        writer.Writer.WriteByte(0);
        writer.Writer.WriteByte(0);
        writer.Writer.WriteVarInt(0);

        var decoded = AddEntityCodec.Instance.DecodeAs<AddEntityPacket>(
            _play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(1, decoded.EntityId);
        Assert.Equal(72, decoded.EntityTypeId);
        Assert.Equal(0.0, decoded.X);
        Assert.Equal(0.5, decoded.Y);
        Assert.Equal(0.0, decoded.MovementX);
        Assert.Equal(0, decoded.Data);
    }

    [Fact]
    public void AddEntityTruncatedThrows()
    {
        byte[] payload = { 1, 0x01 }; // id only
        payload.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void SetEntityDataRoundTrips()
    {
        var original = new SetEntityDataPacket(33, [new EntityDataItemValue(0, 7, 1, 54)]);
        var decoded = SetEntityDataCodec.Instance.DecodeAs<SetEntityDataPacket>(
            _play, PacketDirection.Clientbound, SetEntityDataCodec.Instance.Encode(original));

        Assert.Equal(33, decoded.EntityId);
        Assert.Single(decoded.Values);
        Assert.Equal(0, decoded.Values[0].Id);
        Assert.Equal(7, decoded.Values[0].SerializerId);
        Assert.Equal(1, decoded.Values[0].Count);
        Assert.Equal(54, decoded.Values[0].ItemId);
    }

    [Fact]
    public void SetEntityDataKnownWireForm()
    {
        // Packet ID 101, entity id 7, entity data: field id 8, serializer 7 (item stack),
        // count 1, item 54, empty component patch (0, 0), then EOF 255.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(101);
        writer.Writer.WriteVarInt(7);   // entity id
        writer.Writer.WriteByte(8);     // field id (ItemEntity.DATA_ITEM)
        writer.Writer.WriteVarInt(7);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteVarInt(54);
        writer.Writer.WriteVarInt(0);   // component patch: positive count
        writer.Writer.WriteVarInt(0);   // component patch: negative count
        writer.Writer.WriteByte(255);

        var decoded = SetEntityDataCodec.Instance.DecodeAs<SetEntityDataPacket>(
            _play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(7, decoded.EntityId);
        Assert.Single(decoded.Values);
        Assert.Equal(8, decoded.Values[0].Id);
        Assert.Equal(54, decoded.Values[0].ItemId);
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, AddEntityCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, AddEntityCodec.Instance.Direction);
        Assert.Equal(1, AddEntityCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetEntityDataCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SetEntityDataCodec.Instance.Direction);
        Assert.Equal(101, SetEntityDataCodec.Instance.PacketId);
    }
}
