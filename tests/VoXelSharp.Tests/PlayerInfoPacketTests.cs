using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Nbt;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class PlayerInfoPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    // ---- Player Info Remove (clientbound, id 70) ----

    [Fact]
    public void PlayerInfoRemoveRoundTrips()
    {
        var original = new PlayerInfoRemovePacket(
        [
            new PlayerUuid(0x0123456789ABCDEF, -2742139916407285296),
            new PlayerUuid(0, 0),
        ]);
        using var writer = new PacketWriterScope();

        PlayerInfoRemoveCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoRemovePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(2, decoded.Uuids.Length);
        Assert.Equal(0x0123456789ABCDEF, decoded.Uuids[0].Msb);
        Assert.Equal(-2742139916407285296, decoded.Uuids[0].Lsb);
        Assert.Equal((0, 0), (decoded.Uuids[1].Msb, decoded.Uuids[1].Lsb));
    }

    [Fact]
    public void PlayerInfoRemoveKnownWireForm()
    {
        // Packet ID 70, count 1, UUID = (0x1122334455667788, 0x99AABBCCDDEEFF00 as signed).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(70);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteLong(0x1122334455667788);
        writer.Writer.WriteLong(-7366731831488846080L); // 0x99AABBCCDDEEFF00

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoRemovePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Single(decoded.Uuids);
        Assert.Equal(0x1122334455667788, decoded.Uuids[0].Msb);
        Assert.Equal(-7366731831488846080L, decoded.Uuids[0].Lsb);
    }

    [Fact]
    public void PlayerInfoRemoveEmptyListRoundTrips()
    {
        using var writer = new PacketWriterScope();
        PlayerInfoRemoveCodec.Instance.Encode(new PlayerInfoRemovePacket([]), writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoRemovePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Empty(decoded.Uuids);
    }

    [Fact]
    public void PlayerInfoRemoveTruncatedUuid()
    {
        // Packet ID 70, count 1, but only 4 of the 8 msb bytes follow.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(70);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteBytes(new byte[4]);

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void PlayerInfoRemoveNegativeCount()
    {
        // Packet ID 70, count -1.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(70);
        writer.Writer.WriteVarInt(-1);

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    // ---- Player Info Update (clientbound, id 71) ----

    [Fact]
    public void PlayerInfoUpdateAddPlayerRoundTrips()
    {
        var profile = new PlayerInfoEntryData(
            1, 2,
            new PlayerInfoAddPlayerData("Player1", [new ProfileProperty("textures", "ew0K", null)]),
            null, null, null, null, null, null, null);
        var original = new PlayerInfoUpdatePacket(PlayerInfoActions.AddPlayer, [profile]);
        using var writer = new PacketWriterScope();

        PlayerInfoUpdateCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoUpdatePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(PlayerInfoActions.AddPlayer, decoded.Actions);
        Assert.Single(decoded.Players);
        Assert.Equal(1, decoded.Players[0].UuidMsb);
        PlayerInfoAddPlayerData addPlayer = decoded.Players[0].AddPlayer!;
        Assert.Equal("Player1", addPlayer.Name);
        Assert.Single(addPlayer.Properties);
        Assert.Equal("textures", addPlayer.Properties[0].Name);
        Assert.Equal("ew0K", addPlayer.Properties[0].Value);
        Assert.Null(addPlayer.Properties[0].Signature);
    }

    [Fact]
    public void PlayerInfoUpdateAddPlayerKnownWireForm()
    {
        // Packet ID 71, Actions 0x01 (Add Player), count 1, UUID (0,0),
        // Name "U", properties count 0.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(71);
        writer.Writer.WriteByte(0x01);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteString("U", PlayerInfoUpdatePacket.MaxNameLength);
        writer.Writer.WriteVarInt(0);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoUpdatePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(PlayerInfoActions.AddPlayer, decoded.Actions);
        PlayerInfoAddPlayerData addPlayer = decoded.Players[0].AddPlayer!;
        Assert.Equal("U", addPlayer.Name);
        Assert.Empty(addPlayer.Properties);
    }

    [Fact]
    public void PlayerInfoUpdateMultipleActionsAndPlayers()
    {
        var first = new PlayerInfoEntryData(
            -6148914691236517206L, 1,
            new PlayerInfoAddPlayerData("Alpha", []),
            null,
            (int)PlayerGameMode.Creative,
            true,
            45,
            null,
            0,
            false);
        var second = new PlayerInfoEntryData(
            -4919131752989213765L, 2,
            new PlayerInfoAddPlayerData("Beta", []),
            null,
            (int)PlayerGameMode.Survival,
            true,
            120,
            null,
            1,
            true);
        var original = new PlayerInfoUpdatePacket(
            PlayerInfoActions.AddPlayer | PlayerInfoActions.UpdateGameMode | PlayerInfoActions.UpdateListed
                | PlayerInfoActions.UpdateLatency | PlayerInfoActions.UpdateListOrder | PlayerInfoActions.UpdateHat,
            [first, second]);
        using var writer = new PacketWriterScope();

        PlayerInfoUpdateCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoUpdatePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(2, decoded.Players.Length);
        Assert.Equal("Alpha", decoded.Players[0].AddPlayer!.Name);
        Assert.Equal(PlayerGameMode.Creative, (PlayerGameMode)decoded.Players[0].GameMode!);
        Assert.True(decoded.Players[0].Listed);
        Assert.Equal(45, decoded.Players[0].Ping);
        Assert.Equal(0, decoded.Players[0].ListPriority);
        Assert.False(decoded.Players[0].HatVisible);

        Assert.Equal(2, decoded.Players[1].UuidLsb);
        Assert.Equal("Beta", decoded.Players[1].AddPlayer!.Name);
        Assert.Equal(PlayerGameMode.Survival, (PlayerGameMode)decoded.Players[1].GameMode!);
        Assert.Equal(120, decoded.Players[1].Ping);
        Assert.Equal(1, decoded.Players[1].ListPriority);
        Assert.True(decoded.Players[1].HatVisible);
    }

    [Fact]
    public void PlayerInfoUpdateInitializeChatRoundTrips()
    {
        var profile = new PlayerInfoEntryData(
            7, 8,
            null,
            new PlayerInfoChatSessionData(9, 10, 1234567890123, [1, 2, 3], [4, 5, 6, 7]),
            null, null, null, null, null, null);
        var original = new PlayerInfoUpdatePacket(PlayerInfoActions.InitializeChat, [profile]);
        using var writer = new PacketWriterScope();

        PlayerInfoUpdateCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoUpdatePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        var chat = decoded.Players[0].ChatSession!;
        Assert.Equal(9, chat.SessionIdMsb);
        Assert.Equal(1234567890123, chat.PublicKeyExpiry);
        Assert.Equal([1, 2, 3], chat.EncodedPublicKey);
        Assert.Equal([4, 5, 6, 7], chat.PublicKeySignature);
    }

    [Fact]
    public void PlayerInfoUpdateDisplayNameRoundTrips()
    {
        var original = new PlayerInfoUpdatePacket(
            PlayerInfoActions.UpdateDisplayName,
            [new PlayerInfoEntryData(0, 0, null, null, null, null, null, new NbtString("nick"), null, null)]);
        using var writer = new PacketWriterScope();

        PlayerInfoUpdateCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoUpdatePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal("nick", Assert.IsType<NbtString>(decoded.Players[0].DisplayName!).Value);
    }

    [Fact]
    public void PlayerInfoUpdateNoActionsRoundTrips()
    {
        // Action byte 0x00 with a single bare UUID, as the wire allows.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(71);
        writer.Writer.WriteByte(0x00);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteLong(5);
        writer.Writer.WriteLong(6);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PlayerInfoUpdatePacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(PlayerInfoActions.None, decoded.Actions);
        Assert.Single(decoded.Players);
        Assert.Equal(5, decoded.Players[0].UuidMsb);
        Assert.Null(decoded.Players[0].AddPlayer);
    }

    [Fact]
    public void PlayerInfoUpdateNegativePlayerCountRejected()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(71);
        writer.Writer.WriteByte(0x01); // Add Player
        writer.Writer.WriteVarInt(-1); // count

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void PlayerInfoUpdateExcessPlayerCountRejected()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(71);
        writer.Writer.WriteByte(0x00);
        writer.Writer.WriteVarInt(PlayerInfoUpdatePacket.MaxPlayersCount + 1);

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void PlayerInfoUpdateTruncatedProfileRejected()
    {
        // Add Player declared but the name/properties are cut off.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(71);
        writer.Writer.WriteByte(0x01);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteString("U", PlayerInfoUpdatePacket.MaxNameLength);
        // properties count omitted (truncated)

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void PlayerInfoUpdateInvalidGameModeRejected()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(71);
        writer.Writer.WriteByte(0x04); // Update Game Mode
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteVarInt(9); // invalid game mode

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void PlayerInfoUpdateChatKeyTooLongRejected()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(71);
        writer.Writer.WriteByte(0x02); // Initialize Chat
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteBoolean(true); // present
        writer.Writer.WriteLong(1);
        writer.Writer.WriteLong(2);
        writer.Writer.WriteLong(3);
        writer.Writer.WriteVarInt(PlayerInfoChatSessionData.MaxEncodedPublicKeyLength + 1); // too long

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void PlayerInfoUpdateEncodeRejectsMissingActionData()
    {
        var original = new PlayerInfoUpdatePacket(
            PlayerInfoActions.UpdateGameMode,
            [new PlayerInfoEntryData(0, 0, null, null, null, null, null, null, null, null)]);
        using var writer = new PacketWriterScope();

        Assert.Throws<InvalidDataException>(() =>
            PlayerInfoUpdateCodec.Instance.Encode(original, writer.Writer));
    }

    // ---- Codec metadata ----

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, PlayerInfoUpdateCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, PlayerInfoUpdateCodec.Instance.Direction);
        Assert.Equal(71, PlayerInfoUpdateCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayerInfoRemoveCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, PlayerInfoRemoveCodec.Instance.Direction);
        Assert.Equal(70, PlayerInfoRemoveCodec.Instance.PacketId);
    }
}
