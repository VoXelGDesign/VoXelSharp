using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class SetPlayerInventoryPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void SetPlayerInventoryRoundTrips()
    {
        var original = new SetPlayerInventoryPacket(3, 1, 54);
        var decoded = SetPlayerInventoryCodec.Instance.DecodeAs<SetPlayerInventoryPacket>(
            _play, PacketDirection.Clientbound, SetPlayerInventoryCodec.Instance.Encode(original));

        Assert.Equal(3, decoded.Slot);
        Assert.Equal(1, decoded.Count);
        Assert.Equal(54, decoded.ItemId);
    }

    [Fact]
    public void EmptySlotRoundTrips()
    {
        var original = new SetPlayerInventoryPacket(0, 0, 0);
        var decoded = SetPlayerInventoryCodec.Instance.DecodeAs<SetPlayerInventoryPacket>(
            _play, PacketDirection.Clientbound, SetPlayerInventoryCodec.Instance.Encode(original));

        Assert.Equal(0, decoded.Slot);
        Assert.Equal(0, decoded.Count);
        Assert.Equal(0, decoded.ItemId);
    }

    [Fact]
    public void SetPlayerInventoryKnownWireForm()
    {
        // Packet ID 110, slot 0, count 1, item 54 (grass_block), empty patch (0, 0).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(110);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteVarInt(54);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(0);

        var decoded = SetPlayerInventoryCodec.Instance.DecodeAs<SetPlayerInventoryPacket>(
            _play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(0, decoded.Slot);
        Assert.Equal(1, decoded.Count);
        Assert.Equal(54, decoded.ItemId);
    }

    [Fact]
    public void SetPlayerInventoryTruncatedThrows()
    {
        byte[] payload = { 110, 0x03 };
        payload.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, SetPlayerInventoryCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SetPlayerInventoryCodec.Instance.Direction);
        Assert.Equal(110, SetPlayerInventoryCodec.Instance.PacketId);
    }
}
