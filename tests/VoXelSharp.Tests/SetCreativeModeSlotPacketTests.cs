using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class SetCreativeModeSlotPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void SetCreativeModeSlotRoundTrips()
    {
        var original = new SetCreativeModeSlotPacket(2, new CreativeSlotItem(1, 55, [0x01, 0x02]));
        var decoded = SetCreativeModeSlotCodec.Instance.DecodeAs<SetCreativeModeSlotPacket>(
            _play, PacketDirection.Serverbound, SetCreativeModeSlotCodec.Instance.Encode(original));

        Assert.Equal((short)2, decoded.Slot);
        Assert.Equal(1, decoded.Item.Count);
        Assert.Equal(55, decoded.Item.ItemId);
        Assert.Equal([0x01, 0x02], decoded.Item.Components);
    }

    [Fact]
    public void SetCreativeModeSlotKnownWireForm()
    {
        // Packet ID 57, slot 0, item count 1, item registry ID 1 (stone), no component bytes.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(57);
        writer.Writer.WriteShort(0);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteVarInt(1);

        var decoded = SetCreativeModeSlotCodec.Instance.DecodeAs<SetCreativeModeSlotPacket>(
            _play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal((short)0, decoded.Slot);
        Assert.False(decoded.Item.IsEmpty);
        Assert.Equal(1, decoded.Item.ItemId);
        Assert.Empty(decoded.Item.Components);
    }

    [Fact]
    public void EmptyItemRoundTrips()
    {
        var original = new SetCreativeModeSlotPacket(3, new CreativeSlotItem(0, 0, []));
        var decoded = SetCreativeModeSlotCodec.Instance.DecodeAs<SetCreativeModeSlotPacket>(
            _play, PacketDirection.Serverbound, SetCreativeModeSlotCodec.Instance.Encode(original));

        Assert.True(decoded.Item.IsEmpty);
    }

    [Fact]
    public void EmptyItemKnownWireFormIsJustAZeroCount()
    {
        // Packet ID 57, slot 1, empty item (count 0).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(57);
        writer.Writer.WriteShort(1);
        writer.Writer.WriteVarInt(0);

        var decoded = SetCreativeModeSlotCodec.Instance.DecodeAs<SetCreativeModeSlotPacket>(
            _play, PacketDirection.Serverbound, writer.Bytes);

        Assert.True(decoded.Item.IsEmpty);
        Assert.Empty(decoded.Item.Components);
    }

    [Fact]
    public void SetCreativeModeSlotTruncatedThrows()
    {
        byte[] payload = { 57, 0x00 }; // slot only
        payload.AssertInvalidData(_play, PacketDirection.Serverbound);
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, SetCreativeModeSlotCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, SetCreativeModeSlotCodec.Instance.Direction);
        Assert.Equal(57, SetCreativeModeSlotCodec.Instance.PacketId);
    }
}
