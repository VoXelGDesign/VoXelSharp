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

    // ---- Set Player Position (serverbound, id 30) ----

    [Fact]
    public void SetPlayerPositionRoundTrips()
    {
        var original = new SetPlayerPositionPacket(1.2, 64.0, -3.4, 0x01);
        using var writer = new PacketWriterScope();

        SetPlayerPositionCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetPlayerPositionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1.2, decoded.X);
        Assert.Equal(64.0, decoded.FeetY);
        Assert.Equal(-3.4, decoded.Z);
        Assert.Equal(0x01, decoded.Flags);
    }

    [Fact]
    public void SetPlayerPositionKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(30);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(64.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteByte(0x03);

        var decoded = Assert.IsType<SetPlayerPositionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(64.0, decoded.FeetY);
        Assert.Equal(0x03, decoded.Flags);
    }

    [Fact]
    public void SetPlayerPositionTruncated()
    {
        byte[] payload = [30, 0x00, 0x00]; // only two bytes after the ID

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));
    }

    // ---- Set Player Rotation (serverbound, id 32) ----

    [Fact]
    public void SetPlayerRotationRoundTrips()
    {
        var original = new SetPlayerRotationPacket(90.0f, -5.0f, 0x02);
        using var writer = new PacketWriterScope();

        SetPlayerRotationCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetPlayerRotationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(90.0f, decoded.Yaw);
        Assert.Equal(-5.0f, decoded.Pitch);
        Assert.Equal(0x02, decoded.Flags);
    }

    [Fact]
    public void SetPlayerRotationKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(32);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteByte(0x00);

        var decoded = Assert.IsType<SetPlayerRotationPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0.0f, decoded.Yaw);
        Assert.Equal(0x00, decoded.Flags);
    }

    [Fact]
    public void SetPlayerRotationTruncated()
    {
        byte[] payload = [32, 0x00]; // only one byte after the ID

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));
    }

    // ---- Set Player Movement Flags (serverbound, id 33) ----

    [Fact]
    public void SetPlayerStatusOnlyRoundTrips()
    {
        var original = new SetPlayerStatusOnlyPacket(0x03);
        using var writer = new PacketWriterScope();

        SetPlayerStatusOnlyCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetPlayerStatusOnlyPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x03, decoded.Flags);
    }

    [Fact]
    public void SetPlayerStatusOnlyKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(33);
        writer.Writer.WriteByte(0x01);

        var decoded = Assert.IsType<SetPlayerStatusOnlyPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x01, decoded.Flags);
    }

    [Fact]
    public void SetPlayerStatusOnlyTruncated()
    {
        byte[] payload = [33]; // flags byte missing

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));
    }

    // ---- Punch (serverbound, id 46) ----

    [Fact]
    public void PunchRoundTrips()
    {
        using var writer = new PacketWriterScope();
        PunchCodec.Instance.Encode(new PunchPacket(), writer.Writer);

        var decoded = Assert.IsType<PunchPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void PunchKnownWireForm()
    {
        byte[] payload = { 46 };

        var decoded = Assert.IsType<PunchPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void PunchTrailingBytesThrows()
    {
        byte[] payload = { 46, 0x00 };

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, payload));
    }

    // ---- Player Command (serverbound, id 42) ----

    [Fact]
    public void PlayerCommandRoundTrips()
    {
        var original = new PlayerCommandPacket(1, 2, 0);
        using var writer = new PacketWriterScope();

        PlayerCommandCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<PlayerCommandPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1, decoded.EntityId);
        Assert.Equal(2, decoded.ActionId);
        Assert.Equal(0, decoded.JumpBoost);
    }

    [Fact]
    public void PlayerCommandKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(42);
        writer.Writer.WriteVarInt(7);
        writer.Writer.WriteVarInt(1); // START_SPRINTING
        writer.Writer.WriteVarInt(0);

        var decoded = Assert.IsType<PlayerCommandPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1, decoded.ActionId);
    }

    [Fact]
    public void PlayerCommandInvalidActionThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(42);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(99); // invalid action
        writer.Writer.WriteVarInt(0);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Set Carried Item (serverbound, id 54) ----

    [Fact]
    public void SetCarriedItemRoundTrips()
    {
        var original = new SetCarriedItemPacket(4);
        using var writer = new PacketWriterScope();

        SetCarriedItemCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<SetCarriedItemPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(4, decoded.Slot);
    }

    [Fact]
    public void SetCarriedItemKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(54);
        writer.Writer.WriteShort(0);

        var decoded = Assert.IsType<SetCarriedItemPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.Slot);
    }

    // ---- Player Action (serverbound, id 41) ----

    [Fact]
    public void PlayerActionRoundTrips()
    {
        var original = new PlayerActionPacket(0, new BlockPosition(8, 64, -16), 1, 7);
        using var writer = new PacketWriterScope();

        PlayerActionCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<PlayerActionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.Status);
        Assert.Equal(new BlockPosition(8, 64, -16), decoded.Location);
        Assert.Equal(1, decoded.Face);
        Assert.Equal(7, decoded.Sequence);
    }

    [Fact]
    public void PlayerActionKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(41);
        writer.Writer.WriteVarInt(5); // DROP_ITEM
        writer.Writer.WritePosition(new BlockPosition(0, 64, 0));
        writer.Writer.WriteByte(0);
        writer.Writer.WriteVarInt(0);

        var decoded = Assert.IsType<PlayerActionPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(5, decoded.Status);
    }

    [Fact]
    public void PlayerActionInvalidStatusThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(41);
        writer.Writer.WriteVarInt(99); // invalid status
        writer.Writer.WritePosition(new BlockPosition(0, 64, 0));
        writer.Writer.WriteByte(0);
        writer.Writer.WriteVarInt(0);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    [Fact]
    public void PlayerActionInvalidFaceThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(41);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WritePosition(new BlockPosition(0, 64, 0));
        writer.Writer.WriteByte(6); // invalid face
        writer.Writer.WriteVarInt(0);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Use Item On (serverbound, id 66) ----

    [Fact]
    public void UseItemOnRoundTrips()
    {
        var original = new UseItemOnPacket(0, new BlockPosition(8, 64, -16), 1, 0.5f, 0.25f, 0.75f, false, false, 7);
        using var writer = new PacketWriterScope();

        UseItemOnCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<UseItemOnPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.Hand);
        Assert.Equal(new BlockPosition(8, 64, -16), decoded.Location);
        Assert.Equal(1, decoded.Face);
        Assert.Equal(0.5f, decoded.CursorX);
        Assert.Equal(0.25f, decoded.CursorY);
        Assert.Equal(0.75f, decoded.CursorZ);
        Assert.False(decoded.InsideBlock);
        Assert.False(decoded.WorldBorderHit);
        Assert.Equal(7, decoded.Sequence);
    }

    [Fact]
    public void UseItemOnKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(66);
        writer.Writer.WriteVarInt(0); // main hand
        writer.Writer.WritePosition(new BlockPosition(0, 64, 0));
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(0);

        var decoded = Assert.IsType<UseItemOnPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.Hand);
        Assert.Equal(new BlockPosition(0, 64, 0), decoded.Location);
    }

    [Fact]
    public void UseItemOnInvalidHandThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(66);
        writer.Writer.WriteVarInt(2); // invalid hand
        writer.Writer.WritePosition(new BlockPosition(0, 64, 0));
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(0);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Use Item (serverbound, id 67) ----

    [Fact]
    public void UseItemRoundTrips()
    {
        var original = new UseItemPacket(1, 3, 45.0f, -10.0f);
        using var writer = new PacketWriterScope();

        UseItemCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<UseItemPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1, decoded.Hand);
        Assert.Equal(3, decoded.Sequence);
        Assert.Equal(45.0f, decoded.Yaw);
        Assert.Equal(-10.0f, decoded.Pitch);
    }

    [Fact]
    public void UseItemKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(67);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);

        var decoded = Assert.IsType<UseItemPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.Hand);
    }

    [Fact]
    public void UseItemInvalidHandThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(67);
        writer.Writer.WriteVarInt(9); // invalid hand
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Attack (serverbound, id 1) ----

    [Fact]
    public void AttackRoundTrips()
    {
        var original = new AttackPacket(42);
        using var writer = new PacketWriterScope();

        AttackCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<AttackPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(42, decoded.EntityId);
    }

    [Fact]
    public void AttackKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteVarInt(7);

        var decoded = Assert.IsType<AttackPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(7, decoded.EntityId);
    }

    // ---- Client Command (serverbound, id 12) ----

    [Fact]
    public void ClientCommandRoundTrips()
    {
        var original = new ClientCommandPacket(0); // PERFORM_RESPAWN
        using var writer = new PacketWriterScope();

        ClientCommandCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<ClientCommandPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.ActionId);
    }

    [Fact]
    public void ClientCommandKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(12);
        writer.Writer.WriteVarInt(1); // REQUEST_STATS

        var decoded = Assert.IsType<ClientCommandPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(1, decoded.ActionId);
    }

    [Fact]
    public void ClientCommandInvalidActionThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(12);
        writer.Writer.WriteVarInt(9); // invalid

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Container Click (serverbound, id 18) ----

    [Fact]
    public void ContainerClickRoundTrips()
    {
        var original = new ContainerClickPacket(
            containerId: 0,
            stateId: 3,
            slotNum: 5,
            buttonNum: 0,
            inputId: 0,
            changedSlots:
            [
                new SlotHashEntry(3, HashedStackData.Empty),
            ],
            carriedItem: new HashedStackData(
                true,
                ItemId: 1,
                Count: 2,
                Added: [new HashedComponentHash(5, 0x01020304)],
                Removed: []));

        using var writer = new PacketWriterScope();
        ContainerClickCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<ContainerClickPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.ContainerId);
        Assert.Equal(3, decoded.StateId);
        Assert.Equal(5, decoded.SlotNum);
        Assert.Equal(0, decoded.ButtonNum);
        Assert.Equal(0, decoded.InputId);
        var slot = Assert.Single(decoded.ChangedSlots);
        Assert.Equal(3, slot.Slot);
        Assert.False(slot.Stack.Present);
        Assert.True(decoded.CarriedItem.Present);
        Assert.Equal(1, decoded.CarriedItem.ItemId);
        Assert.Equal(2, decoded.CarriedItem.Count);
        Assert.Equal(new HashedComponentHash(5, 0x01020304), Assert.Single(decoded.CarriedItem.Added));
        Assert.Empty(decoded.CarriedItem.Removed);
    }

    [Fact]
    public void ContainerClickKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(18);
        writer.Writer.WriteVarInt(0); // container id
        writer.Writer.WriteVarInt(0); // state id
        writer.Writer.WriteShort(0); // slot
        writer.Writer.WriteByte(0); // button
        writer.Writer.WriteVarInt(0); // PICKUP
        writer.Writer.WriteVarInt(0); // no changed slots
        writer.Writer.WriteBoolean(false); // empty carried item

        var decoded = Assert.IsType<ContainerClickPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.ContainerId);
        Assert.Empty(decoded.ChangedSlots);
        Assert.False(decoded.CarriedItem.Present);
    }

    [Fact]
    public void ContainerClickInvalidInputThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(18);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteShort(0);
        writer.Writer.WriteByte(0);
        writer.Writer.WriteVarInt(99); // invalid input
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteBoolean(false);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    [Fact]
    public void ContainerClickNegativeContainerIdThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(18);
        writer.Writer.WriteVarInt(-1); // invalid container id

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Container Close (serverbound, id 19) ----

    [Fact]
    public void ContainerCloseRoundTrips()
    {
        var original = new ContainerClosePacket(2);
        using var writer = new PacketWriterScope();

        ContainerCloseCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<ContainerClosePacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(2, decoded.ContainerId);
    }

    [Fact]
    public void ContainerCloseKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(19);
        writer.Writer.WriteVarInt(0);

        var decoded = Assert.IsType<ContainerClosePacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.ContainerId);
    }

    [Fact]
    public void ContainerCloseNegativeContainerIdThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(19);
        writer.Writer.WriteVarInt(-1);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Move Vehicle (serverbound, id 34) ----

    [Fact]
    public void MoveVehicleRoundTrips()
    {
        var original = new MoveVehiclePacket(10.5, 64.0, -30.25, 90.0f, -5.0f, true);
        using var writer = new PacketWriterScope();

        MoveVehicleCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<MoveVehiclePacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(10.5, decoded.X);
        Assert.Equal(64.0, decoded.Y);
        Assert.Equal(-30.25, decoded.Z);
        Assert.Equal(90.0f, decoded.Yaw);
        Assert.Equal(-5.0f, decoded.Pitch);
        Assert.True(decoded.OnGround);
    }

    [Fact]
    public void MoveVehicleKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(34);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteBoolean(false);

        var decoded = Assert.IsType<MoveVehiclePacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.False(decoded.OnGround);
    }

    [Fact]
    public void MoveVehicleNonFiniteThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(34);
        writer.Writer.WriteDouble(double.NaN);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteDouble(0.0);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteFloat(0.0f);
        writer.Writer.WriteBoolean(false);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));
    }

    // ---- Player Abilities (serverbound, id 40) ----

    [Fact]
    public void ServerPlayerAbilitiesRoundTrips()
    {
        var original = new ServerPlayerAbilitiesPacket(0x02);
        using var writer = new PacketWriterScope();

        ServerPlayerAbilitiesCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<ServerPlayerAbilitiesPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x02, decoded.Flags);
    }

    [Fact]
    public void ServerPlayerAbilitiesKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(40);
        writer.Writer.WriteByte(0x00);

        var decoded = Assert.IsType<ServerPlayerAbilitiesPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x00, decoded.Flags);
    }

    // ---- Player Input (serverbound, id 43) ----

    [Fact]
    public void PlayerInputRoundTrips()
    {
        var original = new PlayerInputPacket(0x01);
        using var writer = new PacketWriterScope();

        PlayerInputCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<PlayerInputPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x01, decoded.Flags);
    }

    [Fact]
    public void PlayerInputKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(43);
        writer.Writer.WriteByte(0x41); // forward + sprint

        var decoded = Assert.IsType<PlayerInputPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0x41, decoded.Flags);
    }

    // ---- Recipe Book Change Settings (serverbound, id 47) ----

    [Fact]
    public void RecipeBookChangeSettingsRoundTrips()
    {
        var original = new RecipeBookChangeSettingsPacket(0, true, false);
        using var writer = new PacketWriterScope();

        RecipeBookChangeSettingsCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<RecipeBookChangeSettingsPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(0, decoded.BookType);
        Assert.True(decoded.IsOpen);
        Assert.False(decoded.IsFiltering);
    }

    [Fact]
    public void RecipeBookChangeSettingsKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(47);
        writer.Writer.WriteVarInt(3); // SMOKER
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(true);

        var decoded = Assert.IsType<RecipeBookChangeSettingsPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(3, decoded.BookType);
        Assert.True(decoded.IsFiltering);
    }

    [Fact]
    public void RecipeBookChangeSettingsInvalidBookThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(47);
        writer.Writer.WriteVarInt(9); // invalid book
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(false);

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

        Assert.Equal(ConnectionState.Play, AttackCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, AttackCodec.Instance.Direction);
        Assert.Equal(1, AttackCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ClientCommandCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ClientCommandCodec.Instance.Direction);
        Assert.Equal(12, ClientCommandCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ContainerClickCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ContainerClickCodec.Instance.Direction);
        Assert.Equal(18, ContainerClickCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ContainerCloseCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ContainerCloseCodec.Instance.Direction);
        Assert.Equal(19, ContainerCloseCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, MoveVehicleCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, MoveVehicleCodec.Instance.Direction);
        Assert.Equal(34, MoveVehicleCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ServerPlayerAbilitiesCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerPlayerAbilitiesCodec.Instance.Direction);
        Assert.Equal(40, ServerPlayerAbilitiesCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayerInputCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PlayerInputCodec.Instance.Direction);
        Assert.Equal(43, PlayerInputCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, RecipeBookChangeSettingsCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, RecipeBookChangeSettingsCodec.Instance.Direction);
        Assert.Equal(47, RecipeBookChangeSettingsCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetPlayerPositionAndRotationCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, SetPlayerPositionAndRotationCodec.Instance.Direction);
        Assert.Equal(31, SetPlayerPositionAndRotationCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetPlayerPositionCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, SetPlayerPositionCodec.Instance.Direction);
        Assert.Equal(30, SetPlayerPositionCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetPlayerRotationCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, SetPlayerRotationCodec.Instance.Direction);
        Assert.Equal(32, SetPlayerRotationCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetPlayerStatusOnlyCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, SetPlayerStatusOnlyCodec.Instance.Direction);
        Assert.Equal(33, SetPlayerStatusOnlyCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PunchCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PunchCodec.Instance.Direction);
        Assert.Equal(46, PunchCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayerCommandCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PlayerCommandCodec.Instance.Direction);
        Assert.Equal(42, PlayerCommandCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, SetCarriedItemCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, SetCarriedItemCodec.Instance.Direction);
        Assert.Equal(54, SetCarriedItemCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayerActionCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PlayerActionCodec.Instance.Direction);
        Assert.Equal(41, PlayerActionCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, UseItemOnCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, UseItemOnCodec.Instance.Direction);
        Assert.Equal(66, UseItemOnCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, UseItemCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, UseItemCodec.Instance.Direction);
        Assert.Equal(67, UseItemCodec.Instance.PacketId);

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
