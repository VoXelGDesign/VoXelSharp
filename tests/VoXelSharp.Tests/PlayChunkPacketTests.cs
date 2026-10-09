using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class PlayChunkPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    // ---- Set Center Chunk (clientbound, id 96) ----

    [Fact]
    public void SetCenterChunkRoundTrips()
    {
        var original = new SetCenterChunkPacket(12, -34);
        using var writer = new PacketWriterScope();

        SetCenterChunkCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetCenterChunkPacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(12, decoded.ChunkX);
        Assert.Equal(-34, decoded.ChunkZ);
    }

    [Fact]
    public void SetCenterChunkKnownWireForm()
    {
        // Packet ID 96, then VarInt 5 and VarInt 900 (0x84 0x07).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(96);
        writer.Writer.WriteVarInt(5);
        writer.Writer.WriteVarInt(900);

        var decoded = Assert.IsType<SetCenterChunkPacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(5, decoded.ChunkX);
        Assert.Equal(900, decoded.ChunkZ);
    }

    [Fact]
    public void SetCenterChunkNegativeCoordinates()
    {
        var original = new SetCenterChunkPacket(-1, -1);
        using var writer = new PacketWriterScope();

        SetCenterChunkCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetCenterChunkPacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(-1, decoded.ChunkX);
        Assert.Equal(-1, decoded.ChunkZ);
    }

    [Fact]
    public void SetCenterChunkTrailingBytes()
    {
        using var writer = new PacketWriterScope();
        SetCenterChunkCodec.Instance.Encode(new SetCenterChunkPacket(0, 0), writer.Writer);
        writer.Writer.WriteVarInt(99); // trailing garbage

        Assert.Throws<InvalidDataException>(() =>
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    // ---- Set Render Distance (clientbound, id 97) ----

    [Fact]
    public void SetRenderDistanceRoundTrips()
    {
        using var writer = new PacketWriterScope();
        SetRenderDistanceCodec.Instance.Encode(new SetRenderDistancePacket(10), writer.Writer);

        var decoded = Assert.IsType<SetRenderDistancePacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(10, decoded.ViewDistance);
    }

    [Fact]
    public void SetRenderDistanceKnownWireForm()
    {
        // Packet ID 97, then VarInt 8.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(97);
        writer.Writer.WriteVarInt(8);

        var decoded = Assert.IsType<SetRenderDistancePacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(8, decoded.ViewDistance);
    }

    [Fact]
    public void SetRenderDistanceRejectsOutOfRange()
    {
        // Protocol range is 2-32 on encode.
        Assert.Throws<InvalidDataException>(() =>
        {
            using var writer = new PacketWriterScope();
            SetRenderDistanceCodec.Instance.Encode(new SetRenderDistancePacket(33), writer.Writer);
        });
    }

    [Fact]
    public void SetRenderDistanceTruncated()
    {
        byte[] payload = { 0x61 }; // packet ID 97, no body

        Assert.Throws<InvalidDataException>(() =>
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, payload));
    }

    // ---- Chunk Batch Start (clientbound, id 12) ----

    [Fact]
    public void ChunkBatchStartRoundTrips()
    {
        using var writer = new PacketWriterScope();
        ChunkBatchStartCodec.Instance.Encode(new ChunkBatchStartPacket(), writer.Writer);

        var decoded = Assert.IsType<ChunkBatchStartPacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void ChunkBatchStartKnownWireForm()
    {
        byte[] payload = { 0x0C }; // packet ID 12, no fields

        var decoded = Assert.IsType<ChunkBatchStartPacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, payload));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void ChunkBatchStartTrailingBytes()
    {
        byte[] payload = { 0x0C, 0x01 };

        Assert.Throws<InvalidDataException>(() =>
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, payload));
    }

    // ---- Chunk Batch Finished (clientbound, id 11) ----

    [Fact]
    public void ChunkBatchFinishedRoundTrips()
    {
        using var writer = new PacketWriterScope();
        ChunkBatchFinishedCodec.Instance.Encode(new ChunkBatchFinishedPacket(16), writer.Writer);

        var decoded = Assert.IsType<ChunkBatchFinishedPacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(16, decoded.BatchSize);
    }

    [Fact]
    public void ChunkBatchFinishedKnownWireForm()
    {
        // Packet ID 11, then VarInt 9.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(11);
        writer.Writer.WriteVarInt(9);

        var decoded = Assert.IsType<ChunkBatchFinishedPacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(9, decoded.BatchSize);
    }

    [Fact]
    public void ChunkBatchFinishedNegativeBatchSizeThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(11);
        writer.Writer.WriteVarInt(-1);

        Assert.Throws<InvalidDataException>(() =>
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    // ---- Keep Alive (clientbound id 45, serverbound id 28) ----

    [Fact]
    public void PlayKeepAliveClientboundRoundTrips()
    {
        const long id = 0x123456789ABCDEF0;
        using var writer = new PacketWriterScope();
        PlayKeepAliveCodec.Instance.Encode(new PlayKeepAlivePacket(id), writer.Writer);

        var decoded = Assert.IsType<PlayKeepAlivePacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(id, decoded.KeepAliveId);
    }

    [Fact]
    public void PlayKeepAliveServerboundRoundTrips()
    {
        const long id = 42;
        using var writer = new PacketWriterScope();
        ServerPlayKeepAliveCodec.Instance.Encode(new PlayKeepAlivePacket(id), writer.Writer);

        var decoded = Assert.IsType<PlayKeepAlivePacket>(
            CreateRegistry().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(id, decoded.KeepAliveId);
    }

    [Fact]
    public void PlayKeepAliveKnownWireForm()
    {
        // Clientbound: packet ID 45, then Long 1 (big-endian).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(45);
        writer.Writer.WriteLong(1);

        var decoded = Assert.IsType<PlayKeepAlivePacket>(
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(1, decoded.KeepAliveId);
    }

    [Fact]
    public void PlayKeepAliveServerboundKnownWireForm()
    {
        // Serverbound: packet ID 28, then Long 1.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(28);
        writer.Writer.WriteLong(1);

        var decoded = Assert.IsType<PlayKeepAlivePacket>(
            CreateRegistry().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1, decoded.KeepAliveId);
    }

    [Fact]
    public void PlayKeepAliveTruncated()
    {
        byte[] payload = { 45, 0x00, 0x00, 0x00 }; // only 3 of 8 body bytes

        Assert.Throws<InvalidDataException>(() =>
            CreateRegistry().Decode(_play, PacketDirection.Clientbound, payload));
    }

    // ---- Codec metadata vs docs/data/packets.json ----

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, SetCenterChunkCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SetCenterChunkCodec.Instance.Direction);
        Assert.Equal(96, SetCenterChunkCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetRenderDistanceCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SetRenderDistanceCodec.Instance.Direction);
        Assert.Equal(97, SetRenderDistanceCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ChunkBatchStartCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, ChunkBatchStartCodec.Instance.Direction);
        Assert.Equal(12, ChunkBatchStartCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ChunkBatchFinishedCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, ChunkBatchFinishedCodec.Instance.Direction);
        Assert.Equal(11, ChunkBatchFinishedCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayKeepAliveCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, PlayKeepAliveCodec.Instance.Direction);
        Assert.Equal(45, PlayKeepAliveCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ServerPlayKeepAliveCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerPlayKeepAliveCodec.Instance.Direction);
        Assert.Equal(28, ServerPlayKeepAliveCodec.Instance.PacketId);
    }

    private static PacketRegistry CreateRegistry() => PacketRegistry.CreateDefault();
}
