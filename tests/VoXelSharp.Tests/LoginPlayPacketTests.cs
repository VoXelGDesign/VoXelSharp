using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class LoginPlayPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    private static LoginPlayPacket CreateMinimalPacket(bool hasDeathLocation = false)
        => new(
            entityId: 1,
            isHardcore: false,
            dimensionNames: ["minecraft:overworld"],
            maxPlayers: 20,
            viewDistance: 10,
            simulationDistance: 10,
            reducedDebugInfo: false,
            enableRespawnScreen: true,
            doLimitedCrafting: false,
            dimensionType: 0,
            dimensionName: "minecraft:overworld",
            hashedSeed: 0x123456789ABCDEF0,
            gameMode: PlayerGameMode.Creative,
            previousGameMode: OptionalPlayerGameMode.Survival,
            isDebug: false,
            isFlat: true,
            hasDeathLocation: hasDeathLocation,
            deathDimensionName: hasDeathLocation ? "minecraft:overworld" : null,
            deathLocation: hasDeathLocation ? new BlockPosition(1, 2, 3) : null,
            portalCooldown: 0,
            seaLevel: 63,
            onlineMode: false,
            enforcesSecureChat: false);

    [Fact]
    public void LoginPlayRoundTrips()
    {
        var original = CreateMinimalPacket();
        using var writer = new PacketWriterScope();

        LoginPlayCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<LoginPlayPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(1, decoded.EntityId);
        Assert.False(decoded.IsHardcore);
        Assert.Equal(["minecraft:overworld"], decoded.DimensionNames);
        Assert.Equal(20, decoded.MaxPlayers);
        Assert.Equal(10, decoded.ViewDistance);
        Assert.Equal(10, decoded.SimulationDistance);
        Assert.False(decoded.ReducedDebugInfo);
        Assert.True(decoded.EnableRespawnScreen);
        Assert.False(decoded.DoLimitedCrafting);
        Assert.Equal(0, decoded.DimensionType);
        Assert.Equal("minecraft:overworld", decoded.DimensionName);
        Assert.Equal(0x123456789ABCDEF0, decoded.HashedSeed);
        Assert.Equal(PlayerGameMode.Creative, decoded.GameMode);
        Assert.Equal(OptionalPlayerGameMode.Survival, decoded.PreviousGameMode);
        Assert.False(decoded.IsDebug);
        Assert.True(decoded.IsFlat);
        Assert.False(decoded.HasDeathLocation);
        Assert.Null(decoded.DeathDimensionName);
        Assert.Null(decoded.DeathLocation);
        Assert.Equal(0, decoded.PortalCooldown);
        Assert.Equal(63, decoded.SeaLevel);
        Assert.False(decoded.OnlineMode);
        Assert.False(decoded.EnforcesSecureChat);
    }

    [Fact]
    public void LoginPlayRoundTripsWithDeathLocation()
    {
        var original = CreateMinimalPacket(hasDeathLocation: true);
        using var writer = new PacketWriterScope();

        LoginPlayCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<LoginPlayPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.True(decoded.HasDeathLocation);
        Assert.Equal("minecraft:overworld", decoded.DeathDimensionName);
        Assert.Equal(new BlockPosition(1, 2, 3), decoded.DeathLocation);
    }

    [Fact]
    public void LoginPlayKnownWireForm()
    {
        // Hand-built payload for the minimal packet (no death location).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(50); // packet ID
        writer.Writer.WriteInt(1);     // entity ID
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:overworld", LoginPlayPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(20); // max players
        writer.Writer.WriteVarInt(10); // view distance
        writer.Writer.WriteVarInt(10); // simulation distance
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(true); // respawn screen
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(0); // dimension type
        writer.Writer.WriteString("minecraft:overworld", LoginPlayPacket.MaxIdentifierLength);
        writer.Writer.WriteLong(0x123456789ABCDEF0);
        writer.Writer.WriteVarInt(1); // game mode creative
        writer.Writer.WriteVarInt(1); // previous game mode survival
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(true); // flat
        writer.Writer.WriteBoolean(false); // has death location
        writer.Writer.WriteVarInt(0); // portal cooldown
        writer.Writer.WriteVarInt(63); // sea level
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(false);

        var decoded = Assert.IsType<LoginPlayPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.True(decoded.IsFlat);
        Assert.Equal(PlayerGameMode.Creative, decoded.GameMode);
        Assert.Equal(63, decoded.SeaLevel);
    }

    [Fact]
    public void LoginPlayInvalidGameModeThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(50);
        writer.Writer.WriteInt(1);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(20);
        writer.Writer.WriteVarInt(10);
        writer.Writer.WriteVarInt(10);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteString("minecraft:overworld", LoginPlayPacket.MaxIdentifierLength);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteVarInt(9); // invalid game mode
        writer.Writer.WriteVarInt(0);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void LoginPlayNegativeDimensionCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(50);
        writer.Writer.WriteInt(1);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(-1); // negative dimension name count

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void LoginPlayTrailingBytes()
    {
        var original = CreateMinimalPacket();
        using var writer = new PacketWriterScope();
        LoginPlayCodec.Instance.Encode(original, writer.Writer);
        writer.Writer.WriteByte(0xFF);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void LoginPlayTruncated()
    {
        byte[] payload = { 50, 0x00, 0x00 }; // packet ID + 2 bytes only

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, payload));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, LoginPlayCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, LoginPlayCodec.Instance.Direction);
        Assert.Equal(50, LoginPlayCodec.Instance.PacketId);
    }
}
