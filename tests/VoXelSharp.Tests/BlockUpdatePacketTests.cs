using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.World;

namespace VoXelSharp.Tests;

public class BlockUpdatePacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void BlockUpdateRoundTrips()
    {
        var original = new BlockUpdatePacket(new BlockPosition(10, 70, -5), BlockStateIds.GrassBlock);
        var decoded = BlockUpdateCodec.Instance.DecodeAs<BlockUpdatePacket>(
            _play, PacketDirection.Clientbound, BlockUpdateCodec.Instance.Encode(original));

        Assert.Equal(new BlockPosition(10, 70, -5), decoded.Location);
        Assert.Equal(BlockStateIds.GrassBlock, decoded.BlockStateId);
    }

    [Fact]
    public void BlockUpdateKnownWireForm()
    {
        // Packet ID 8, position (1,2,3), block state ID 9 (grass_block).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(8);
        writer.Writer.WritePosition(new BlockPosition(1, 2, 3));
        writer.Writer.WriteVarInt(9);

        var decoded = BlockUpdateCodec.Instance.DecodeAs<BlockUpdatePacket>(
            _play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(new BlockPosition(1, 2, 3), decoded.Location);
        Assert.Equal(9, decoded.BlockStateId);
    }

    [Fact]
    public void BlockUpdateTruncatedThrows()
    {
        var packet = new BlockUpdatePacket(new BlockPosition(0, 100, 0), BlockStateIds.Air);
        byte[] payload = BlockUpdateCodec.Instance.Encode(packet)[..^2]; // drop the block state ID
        payload.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, BlockUpdateCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, BlockUpdateCodec.Instance.Direction);
        Assert.Equal(8, BlockUpdateCodec.Instance.PacketId);
    }
}
