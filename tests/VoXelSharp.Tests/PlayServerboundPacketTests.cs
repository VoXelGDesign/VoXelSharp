using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class PlayServerboundPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    // ---- Confirm Teleportation (serverbound, id 0) ----

    [Fact]
    public void ConfirmTeleportationRoundTrips()
    {
        var original = new ConfirmTeleportationPacket(7, 10.5, 64.0, -30.0, 0.0f, 90.0f);
        using var writer = new PacketWriterScope();

        ConfirmTeleportationCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<ConfirmTeleportationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(7, decoded.TeleportId);
        Assert.Equal(10.5, decoded.X);
        Assert.Equal(64.0, decoded.Y);
        Assert.Equal(-30.0, decoded.Z);
        Assert.Equal(0.0f, decoded.Yaw);
        Assert.Equal(90.0f, decoded.Pitch);
    }

    [Fact]
    public void ConfirmTeleportationKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0); // packet ID
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteDouble(1.0);
        writer.Writer.WriteDouble(2.0);
        writer.Writer.WriteDouble(3.0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);

        var decoded = Assert.IsType<ConfirmTeleportationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1, decoded.TeleportId);
        Assert.Equal(3.0, decoded.Z);
    }

    [Fact]
    public void ConfirmTeleportationTruncated()
    {
        byte[] payload = [0x00, 0x01]; // ID only

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));
    }

    // ---- Set Player Position and Rotation (serverbound, id 31) ----

    [Fact]
    public void SetPlayerPositionAndRotationRoundTrips()
    {
        var original = new SetPlayerPositionAndRotationPacket(1.2, 64.0, -3.4, 45.0f, -10.0f, 0x01);
        using var writer = new PacketWriterScope();

        SetPlayerPositionAndRotationCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetPlayerPositionAndRotationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1.2, decoded.X);
        Assert.Equal(64.0, decoded.FeetY);
        Assert.Equal(-3.4, decoded.Z);
        Assert.Equal(45.0f, decoded.Yaw);
        Assert.Equal(-10.0f, decoded.Pitch);
        Assert.Equal(0x01, decoded.Flags);
    }

    [Fact]
    public void SetPlayerPositionAndRotationKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(31);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(64.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteByte(0x00);

        var decoded = Assert.IsType<SetPlayerPositionAndRotationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(64.0, decoded.FeetY);
        Assert.Equal(0x00, decoded.Flags);
    }

    [Fact]
    public void SetPlayerPositionAndRotationAllFlags()
    {
        using var writer = new PacketWriterScope();
        SetPlayerPositionAndRotationCodec.Instance.Encode(
            new SetPlayerPositionAndRotationPacket(0, 0, 0, 0, 0, 0x03), writer.Writer);

        var decoded = Assert.IsType<SetPlayerPositionAndRotationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x03, decoded.Flags);
    }

    // ---- Chunk Batch Received (serverbound, id 11) ----

    [Fact]
    public void ChunkBatchReceivedRoundTrips()
    {
        using var writer = new PacketWriterScope();
        ChunkBatchReceivedCodec.Instance.Encode(new ChunkBatchReceivedPacket(4.0f), writer.Writer);

        var decoded = Assert.IsType<ChunkBatchReceivedPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(4.0f, decoded.ChunksPerTick);
    }

    [Fact]
    public void ChunkBatchReceivedKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(11);
        writer.Writer.WriteFloat(0.0f);

        var decoded = Assert.IsType<ChunkBatchReceivedPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0.0f, decoded.ChunksPerTick);
    }

    // ---- Player Loaded (serverbound, id 44) ----

    [Fact]
    public void PlayerLoadedRoundTrips()
    {
        using var writer = new PacketWriterScope();
        PlayerLoadedCodec.Instance.Encode(new PlayerLoadedPacket(), writer.Writer);

        var decoded = Assert.IsType<PlayerLoadedPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void PlayerLoadedKnownWireForm()
    {
        byte[] payload = { 44 };

        var decoded = Assert.IsType<PlayerLoadedPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void PlayerLoadedTrailingBytesThrows()
    {
        byte[] payload = { 44, 0x00 };

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));
    }

    // ---- Client Tick End (serverbound, id 13) ----

    [Fact]
    public void ClientTickEndRoundTrips()
    {
        using var writer = new PacketWriterScope();
        ClientTickEndCodec.Instance.Encode(new ClientTickEndPacket(), writer.Writer);

        var decoded = Assert.IsType<ClientTickEndPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void ClientTickEndKnownWireForm()
    {
        byte[] payload = { 13 };

        var decoded = Assert.IsType<ClientTickEndPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));

        Assert.NotNull(decoded);
    }

    // ---- Pong (serverbound, id 45) ----

    [Fact]
    public void PlayPongRoundTrips()
    {
        using var writer = new PacketWriterScope();
        PlayPongCodec.Instance.Encode(new PlayPongPacket(0x01020304), writer.Writer);

        var decoded = Assert.IsType<PlayPongPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x01020304, decoded.Id);
    }

    [Fact]
    public void PlayPongKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(45);
        writer.Writer.WriteInt(-1);

        var decoded = Assert.IsType<PlayPongPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(-1, decoded.Id);
    }

    // ---- Play Client Information (serverbound, id 14) ----

    [Fact]
    public void PlayClientInformationRoundTrips()
    {
        using var writer = new PacketWriterScope();
        PlayClientInformationCodec.Instance.Encode(
            new ClientInformationPacket("en_US", 8, ClientChatMode.Enabled, true, 0x7F, ClientMainHand.Right, false, true, ClientParticleStatus.All),
            writer.Writer);

        var decoded = Assert.IsType<ClientInformationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal("en_US", decoded.Locale);
        Assert.Equal(8, decoded.ViewDistance);
        Assert.Equal(ClientChatMode.Enabled, decoded.ChatMode);
    }

    [Fact]
    public void PlayClientInformationKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(14); // play packet ID
        writer.Writer.WriteString("en_GB", ClientInformationPacket.MaxLocaleLength);
        writer.Writer.WriteByte(10);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteByte(0x7F);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteVarInt(0);

        var decoded = Assert.IsType<ClientInformationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal("en_GB", decoded.Locale);
        Assert.Equal(10, decoded.ViewDistance);
    }

    [Fact]
    public void PlayClientInformationInvalidChatModeThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(14);
        writer.Writer.WriteString("en_US", ClientInformationPacket.MaxLocaleLength);
        writer.Writer.WriteByte(8);
        writer.Writer.WriteVarInt(9); // invalid

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Codec metadata ----

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, ConfirmTeleportationCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ConfirmTeleportationCodec.Instance.Direction);
        Assert.Equal(0, ConfirmTeleportationCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetPlayerPositionAndRotationCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, SetPlayerPositionAndRotationCodec.Instance.Direction);
        Assert.Equal(31, SetPlayerPositionAndRotationCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ChunkBatchReceivedCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ChunkBatchReceivedCodec.Instance.Direction);
        Assert.Equal(11, ChunkBatchReceivedCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayerLoadedCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PlayerLoadedCodec.Instance.Direction);
        Assert.Equal(44, PlayerLoadedCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ClientTickEndCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ClientTickEndCodec.Instance.Direction);
        Assert.Equal(13, ClientTickEndCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayPongCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PlayPongCodec.Instance.Direction);
        Assert.Equal(45, PlayPongCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayClientInformationCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PlayClientInformationCodec.Instance.Direction);
        Assert.Equal(14, PlayClientInformationCodec.Instance.PacketId);
    }
}
