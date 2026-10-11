using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class BlockChangedAckPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void BlockChangedAckRoundTrips()
    {
        var original = new BlockChangedAckPacket(42);
        var decoded = BlockChangedAckCodec.Instance.DecodeAs<BlockChangedAckPacket>(
            _play, PacketDirection.Clientbound, BlockChangedAckCodec.Instance.Encode(original));

        Assert.Equal(42, decoded.Sequence);
    }

    [Fact]
    public void BlockChangedAckKnownWireForm()
    {
        // Packet ID 4, sequence 7.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(4);
        writer.Writer.WriteVarInt(7);

        var decoded = BlockChangedAckCodec.Instance.DecodeAs<BlockChangedAckPacket>(
            _play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(7, decoded.Sequence);
    }

    [Fact]
    public void BlockChangedAckTruncatedThrows()
    {
        byte[] payload = { 0x04 }; // ends before the sequence VarInt
        payload.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, BlockChangedAckCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, BlockChangedAckCodec.Instance.Direction);
        Assert.Equal(4, BlockChangedAckCodec.Instance.PacketId);
    }
}
