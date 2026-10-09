using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class PlaySmallPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    // ---- Synchronize Player Position (clientbound, id 73) ----

    [Fact]
    public void SynchronizePlayerPositionRoundTrips()
    {
        var original = new SynchronizePlayerPositionPacket(
            5, 100.5, 64.0, -200.25, 0.0, 0.0, 0.0, 12.5f, -30.0f,
            TeleportFlags.None);
        using var writer = new PacketWriterScope();

        SynchronizePlayerPositionCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SynchronizePlayerPositionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(5, decoded.TeleportId);
        Assert.Equal(100.5, decoded.X);
        Assert.Equal(64.0, decoded.Y);
        Assert.Equal(-200.25, decoded.Z);
        Assert.Equal(0.0, decoded.VelocityX);
        Assert.Equal(12.5f, decoded.Yaw);
        Assert.Equal(-30.0f, decoded.Pitch);
        Assert.Equal(TeleportFlags.None, decoded.Flags);
    }

    [Fact]
    public void SynchronizePlayerPositionRelativeFlagsRoundTrip()
    {
        var original = new SynchronizePlayerPositionPacket(
            3, 1.0, 2.0, 3.0, 0.1, 0.2, 0.3, 90.0f, 45.0f,
            TeleportFlags.RelativeX | TeleportFlags.RelativeY | TeleportFlags.RelativeZ);
        using var writer = new PacketWriterScope();

        SynchronizePlayerPositionCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SynchronizePlayerPositionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(TeleportFlags.RelativeX | TeleportFlags.RelativeY | TeleportFlags.RelativeZ, decoded.Flags);
        Assert.Equal(0.1, decoded.VelocityX);
    }

    [Fact]
    public void SynchronizePlayerPositionKnownWireForm()
    {
        // Packet ID 73, teleport id 1, X=1, Y=2, Z=3, velocity 0,0,0, yaw=0, pitch=0, flags=0.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(73);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteDouble(1.0);
        writer.Writer.WriteDouble(2.0);
        writer.Writer.WriteDouble(3.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteInt(0);

        var decoded = Assert.IsType<SynchronizePlayerPositionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(1, decoded.TeleportId);
        Assert.Equal(1.0, decoded.X);
        Assert.Equal(3.0, decoded.Z);
    }

    [Fact]
    public void SynchronizePlayerPositionTruncated()
    {
        byte[] payload = [73, 0x01, 0x00, 0x00]; // only teleport id + 2 bytes

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, payload));
    }

    // ---- Set Default Spawn Position (clientbound, id 99) ----

    [Fact]
    public void SetDefaultSpawnPositionRoundTrips()
    {
        var original = new SetDefaultSpawnPositionPacket("minecraft:overworld", new BlockPosition(8, 70, -16), 0.0f, 0.0f);
        using var writer = new PacketWriterScope();

        SetDefaultSpawnPositionCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetDefaultSpawnPositionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal("minecraft:overworld", decoded.DimensionName);
        Assert.Equal(new BlockPosition(8, 70, -16), decoded.Location);
        Assert.Equal(0.0f, decoded.Yaw);
        Assert.Equal(0.0f, decoded.Pitch);
    }

    [Fact]
    public void SetDefaultSpawnPositionKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(99);
        writer.Writer.WriteString("minecraft:overworld", SetDefaultSpawnPositionPacket.MaxIdentifierLength);
        writer.Writer.WritePosition(new BlockPosition(0, 64, 0));
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);

        var decoded = Assert.IsType<SetDefaultSpawnPositionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal("minecraft:overworld", decoded.DimensionName);
        Assert.Equal(new BlockPosition(0, 64, 0), decoded.Location);
    }

    [Fact]
    public void SetDefaultSpawnPositionTruncated()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(99);
        writer.Writer.WriteString("minecraft:overworld", SetDefaultSpawnPositionPacket.MaxIdentifierLength);
        writer.Writer.WritePosition(new BlockPosition(0, 64, 0));
        writer.Writer.WriteFloat(0.0f); // pitch missing

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    // ---- Set Simulation Distance (clientbound, id 113) ----

    [Fact]
    public void SetSimulationDistanceRoundTrips()
    {
        using var writer = new PacketWriterScope();
        SetSimulationDistanceCodec.Instance.Encode(new SetSimulationDistancePacket(10), writer.Writer);

        var decoded = Assert.IsType<SetSimulationDistancePacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(10, decoded.SimulationDistance);
    }

    [Fact]
    public void SetSimulationDistanceKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(113);
        writer.Writer.WriteVarInt(8);

        var decoded = Assert.IsType<SetSimulationDistancePacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(8, decoded.SimulationDistance);
    }

    // ---- Set Held Item (clientbound, id 107) ----

    [Fact]
    public void SetHeldItemRoundTrips()
    {
        using var writer = new PacketWriterScope();
        SetHeldItemCodec.Instance.Encode(new SetHeldItemPacket(4), writer.Writer);

        var decoded = Assert.IsType<SetHeldItemPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(4, decoded.Slot);
    }

    [Fact]
    public void SetHeldItemKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(107);
        writer.Writer.WriteVarInt(0);

        var decoded = Assert.IsType<SetHeldItemPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(0, decoded.Slot);
    }

    [Fact]
    public void SetHeldItemRejectsOutOfRange()
    {
        Assert.Throws<InvalidDataException>(() =>
        {
            using var writer = new PacketWriterScope();
            SetHeldItemCodec.Instance.Encode(new SetHeldItemPacket(9), writer.Writer);
        });
    }

    // ---- Codec metadata ----

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, SynchronizePlayerPositionCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SynchronizePlayerPositionCodec.Instance.Direction);
        Assert.Equal(73, SynchronizePlayerPositionCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetDefaultSpawnPositionCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SetDefaultSpawnPositionCodec.Instance.Direction);
        Assert.Equal(99, SetDefaultSpawnPositionCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetSimulationDistanceCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SetSimulationDistanceCodec.Instance.Direction);
        Assert.Equal(113, SetSimulationDistanceCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetHeldItemCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SetHeldItemCodec.Instance.Direction);
        Assert.Equal(107, SetHeldItemCodec.Instance.PacketId);
    }
}
