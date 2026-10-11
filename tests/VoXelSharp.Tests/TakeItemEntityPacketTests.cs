using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class TakeItemEntityPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void TakeItemEntityRoundTrips()
    {
        var original = new TakeItemEntityPacket(42, 7, 1);
        var decoded = TakeItemEntityCodec.Instance.DecodeAs<TakeItemEntityPacket>(
            _play, PacketDirection.Clientbound, TakeItemEntityCodec.Instance.Encode(original));

        Assert.Equal(42, decoded.ItemEntityId);
        Assert.Equal(7, decoded.PlayerId);
        Assert.Equal(1, decoded.Amount);
    }

    [Fact]
    public void TakeItemEntityKnownWireForm()
    {
        // Packet ID 127, item 5, player 2, amount 1.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(127);
        writer.Writer.WriteVarInt(5);
        writer.Writer.WriteVarInt(2);
        writer.Writer.WriteVarInt(1);

        var decoded = TakeItemEntityCodec.Instance.DecodeAs<TakeItemEntityPacket>(
            _play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(5, decoded.ItemEntityId);
        Assert.Equal(2, decoded.PlayerId);
        Assert.Equal(1, decoded.Amount);
    }

    [Fact]
    public void TakeItemEntityTruncatedThrows()
    {
        byte[] payload = { 127, 0x01 };
        payload.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, TakeItemEntityCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, TakeItemEntityCodec.Instance.Direction);
        Assert.Equal(127, TakeItemEntityCodec.Instance.PacketId);
    }
}
