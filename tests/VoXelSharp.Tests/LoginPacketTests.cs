using System.Text;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class LoginPacketTests
{
    private const ConnectionState _login = ConnectionState.Login;

    // ---- Login Acknowledged (serverbound, no fields) ----

    [Fact]
    public void LoginAcknowledgedRoundTrips()
    {
        using var writer = new PacketWriterScope();

        LoginAcknowledgedCodec.Instance.Encode(new LoginAcknowledgedPacket(), writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginAcknowledgedPacket>(
            registry.Decode(_login, PacketDirection.Serverbound, writer.Bytes));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void LoginAcknowledgedKnownWireForm()
    {
        // Payload: packet ID 3 (0x03), no fields.
        byte[] payload = { 0x03 };

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginAcknowledgedPacket>(
            registry.Decode(_login, PacketDirection.Serverbound, payload));

        Assert.NotNull(decoded);
    }

    [Fact]
    public void LoginAcknowledgedTrailingBytes()
    {
        byte[] payload = { 0x03, 0xAA, 0xBB };

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Serverbound, payload));
    }

    // ---- Disconnect (login) (clientbound) ----

    [Fact]
    public void DisconnectLoginRoundTrips()
    {
        const string reason = """{"text":"Too many players!"}""";
        using var writer = new PacketWriterScope();

        DisconnectLoginCodec.Instance.Encode(new DisconnectLoginPacket(reason), writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<DisconnectLoginPacket>(
            registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(reason, decoded.Reason);
    }

    [Fact]
    public void DisconnectLoginKnownWireForm()
    {
        // Payload: packet ID 0, then String "{"text":"foo"}":
        //   0x00 (id), 0x10 (len=16), then the UTF-8 bytes.
        string reason = """{"text":"foo"}""";
        byte[] reasonUtf8 = Encoding.UTF8.GetBytes(reason);

        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(reasonUtf8.Length);
        writer.Writer.WriteBytes(reasonUtf8);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<DisconnectLoginPacket>(
            registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(reason, decoded.Reason);
    }

    [Fact]
    public void DisconnectLoginTruncatedAfterLength()
    {
        // Payload: id 0, string length 100, but no data follows.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(100);

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void DisconnectLoginTrailingBytes()
    {
        using var writer = new PacketWriterScope();
        DisconnectLoginCodec.Instance.Encode(new DisconnectLoginPacket("bye"), writer.Writer);
        writer.Writer.WriteVarInt(0xFF); // trailing garbage

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void DisconnectLoginMaxReasonLength()
    {
        string maxReason = new string('x', DisconnectLoginPacket.MaxReasonLength);
        using var writer = new PacketWriterScope();

        DisconnectLoginCodec.Instance.Encode(new DisconnectLoginPacket(maxReason), writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<DisconnectLoginPacket>(
            registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(maxReason.Length, decoded.Reason.Length);
    }

    [Fact]
    public void DisconnectLoginExceedsMaxReasonLength()
    {
        string tooLong = new string('x', DisconnectLoginPacket.MaxReasonLength + 1);
        Assert.Throws<ArgumentException>(() => new DisconnectLoginPacket(tooLong));
    }

    // ---- Login Start (serverbound) ----

    [Fact]
    public void LoginStartRoundTrips()
    {
        var original = new LoginStartPacket("Player1", 0x0123456789ABCDEF, -2742139916407285296);
        using var writer = new PacketWriterScope();

        LoginStartCodec.Instance.Encode(original, writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginStartPacket>(
            registry.Decode(_login, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(original.Name, decoded.Name);
        Assert.Equal(original.PlayerUuidMsb, decoded.PlayerUuidMsb);
        Assert.Equal(original.PlayerUuidLsb, decoded.PlayerUuidLsb);
    }

    [Fact]
    public void LoginStartKnownWireForm()
    {
        // Player "x" with UUID msb=0x0123456789ABCDEF, lsb=0xFEDCBA9876543210 (as signed long).
        byte[] nameBytes = Encoding.UTF8.GetBytes("x");

        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);        // packet ID
        writer.Writer.WriteVarInt(nameBytes.Length);
        writer.Writer.WriteBytes(nameBytes); // "x"
        writer.Writer.WriteLong(0x0123456789ABCDEF);
        writer.Writer.WriteLong(-2742139916407285296L); // 0xFEDCBA9876543210 as signed

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginStartPacket>(
            registry.Decode(_login, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal("x", decoded.Name);
        Assert.Equal(0x0123456789ABCDEF, decoded.PlayerUuidMsb);
        Assert.Equal(-2742139916407285296L, decoded.PlayerUuidLsb);
    }

    [Fact]
    public void LoginStartTruncatedName()
    {
        // Payload: id 0, string length 5, but only 2 bytes follow.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteVarInt(5);
        writer.Writer.WriteBytes(Encoding.UTF8.GetBytes("ab"));

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Serverbound, writer.Bytes));
    }

    [Fact]
    public void LoginStartTruncatedUuid()
    {
        // Name present but only 4 of the 8 UUID bytes follow.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteString("n", LoginStartPacket.MaxNameLength);
        writer.Writer.WriteBytes(new byte[4]); // half of msb

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Serverbound, writer.Bytes));
    }

    [Fact]
    public void LoginStartExceedsMaxNameLength()
    {
        string tooLong = new string('x', LoginStartPacket.MaxNameLength + 1);
        Assert.Throws<ArgumentException>(() => new LoginStartPacket(tooLong, 0, 0));
    }

    [Fact]
    public void LoginStartMaxNameLength()
    {
        string maxName = new string('x', LoginStartPacket.MaxNameLength);
        using var writer = new PacketWriterScope();

        LoginStartCodec.Instance.Encode(new LoginStartPacket(maxName, 0, 0), writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginStartPacket>(
            registry.Decode(_login, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(maxName, decoded.Name);
    }

    // ---- Login Success (clientbound, wraps GameProfile) ----

    [Fact]
    public void LoginSuccessRoundTrips()
    {
        var properties = new ProfileProperty[]
        {
            new("textures", "ew0K...", null),
        };
        var profile = new GameProfile(
            -6148914691236517206L, -4919883347990483973L,
            "Player1", properties);
        var original = new LoginSuccessPacket(profile, -3689348815808387380L, -2459625783626286083L);

        using var writer = new PacketWriterScope();
        LoginSuccessCodec.Instance.Encode(original, writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginSuccessPacket>(
            registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(-6148914691236517206L, decoded.Profile.PlayerUuidMsb);
        Assert.Equal(-4919883347990483973L, decoded.Profile.PlayerUuidLsb);
        Assert.Equal("Player1", decoded.Profile.Username);
        Assert.Single(decoded.Profile.Properties);
        Assert.Equal("textures", decoded.Profile.Properties[0].Name);
        Assert.Equal("ew0K...", decoded.Profile.Properties[0].Value);
        Assert.Null(decoded.Profile.Properties[0].Signature);
        Assert.Equal(-3689348815808387380L, decoded.SessionUuidMsb);
        Assert.Equal(-2459625783626286083L, decoded.SessionUuidLsb);
    }

    [Fact]
    public void LoginSuccessWithPropertySignature()
    {
        var profile = new GameProfile(
            1, 2, "TestPlayer",
            [new ProfileProperty("textures", "ew0K", "signature_data")]);
        var original = new LoginSuccessPacket(profile, 3, 4);

        using var writer = new PacketWriterScope();
        LoginSuccessCodec.Instance.Encode(original, writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginSuccessPacket>(
            registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal("signature_data", decoded.Profile.Properties[0].Signature);
    }

    [Fact]
    public void LoginSuccessMaxProperties()
    {
        var props = new ProfileProperty[GameProfile.MaxPropertiesCount];
        for (int i = 0; i < props.Length; i++)
        {
            props[i] = new("key" + i, "value" + i, null);
        }

        var packet = new LoginSuccessPacket(new GameProfile(0, 0, "U", props), 0, 0);

        using var writer = new PacketWriterScope();
        LoginSuccessCodec.Instance.Encode(packet, writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<LoginSuccessPacket>(
            registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(GameProfile.MaxPropertiesCount, decoded.Profile.Properties.Length);
    }

    [Fact]
    public void LoginSuccessPropertyCountExceedsMax()
    {
        // Wire form with 17 properties (max is 16).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(2); // packet ID
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0); // UUID
        writer.Writer.WriteString("U", 16);
        writer.Writer.WriteVarInt(17); // 17 properties — exceeds max

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void LoginSuccessProfileTruncated()
    {
        // Only partial profile data.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(2);
        writer.Writer.WriteLong(0); // msb only, no lsb

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void LoginSuccessTrailingBytes()
    {
        var packet = new LoginSuccessPacket(new GameProfile(0, 0, "U", []), 0, 0);

        using var writer = new PacketWriterScope();
        LoginSuccessCodec.Instance.Encode(packet, writer.Writer);
        writer.Writer.WriteByte(0x99); // trailing garbage

        var registry = CreateRegistry();
        Assert.Throws<InvalidDataException>(
            () => registry.Decode(_login, PacketDirection.Clientbound, writer.Bytes));
    }

    // ---- GameProfile IO (body-level, no packet ID) ----

    [Fact]
    public void GameProfileEmptyProperties()
    {
        var profile = new GameProfile(1, 2, "Player", []);

        using var writer = new PacketWriterScope();
        GameProfileIO.Encode(profile, writer.Writer);

        var reader = new PacketReader(writer.Bytes);
        var decoded = GameProfileIO.Decode(ref reader);

        Assert.Equal(1, decoded.PlayerUuidMsb);
        Assert.Equal(2, decoded.PlayerUuidLsb);
        Assert.Equal("Player", decoded.Username);
        Assert.Empty(decoded.Properties);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void GameProfilePropertySignatureOptional()
    {
        var profile = new GameProfile(0, 0, "U", [new ProfileProperty("a", "b", null)]);

        using var writer = new PacketWriterScope();
        GameProfileIO.Encode(profile, writer.Writer);

        var reader = new PacketReader(writer.Bytes);
        var decoded = GameProfileIO.Decode(ref reader);

        Assert.Null(decoded.Properties[0].Signature);
    }

    [Fact]
    public void GameProfilePropertySignatureRoundTrips()
    {
        var profile = new GameProfile(0, 0, "U", [new ProfileProperty("a", "b", "sig")]);

        using var writer = new PacketWriterScope();
        GameProfileIO.Encode(profile, writer.Writer);

        var reader = new PacketReader(writer.Bytes);
        var decoded = GameProfileIO.Decode(ref reader);

        Assert.Equal("sig", decoded.Properties[0].Signature);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void GameProfileKnownWireForm()
    {
        // Profile: UUID (0, 0), Username "U", one property {Name "a", Value "b", no signature}.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteString("U", GameProfile.MaxUsernameLength);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("a", GameProfile.MaxPropertyNameLength);
        writer.Writer.WriteString("b", GameProfile.MaxPropertyValueLength);
        writer.Writer.WriteBoolean(false); // no signature

        var reader = new PacketReader(writer.Bytes);
        var decoded = GameProfileIO.Decode(ref reader);

        Assert.Single(decoded.Properties);
        Assert.Equal("a", decoded.Properties[0].Name);
        Assert.Equal("b", decoded.Properties[0].Value);
        Assert.Null(decoded.Properties[0].Signature);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void GameProfileSignatureWireForm()
    {
        // One property {Name "a", Value "b", Signature "sig"}.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteString("U", GameProfile.MaxUsernameLength);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("a", GameProfile.MaxPropertyNameLength);
        writer.Writer.WriteString("b", GameProfile.MaxPropertyValueLength);
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteString("sig", GameProfile.MaxSignatureLength);

        var reader = new PacketReader(writer.Bytes);
        var decoded = GameProfileIO.Decode(ref reader);

        Assert.Equal("sig", decoded.Properties[0].Signature);
        Assert.Equal(0, reader.Remaining);
    }

    // ---- Registry / helpers ----

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Login, LoginAcknowledgedCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, LoginAcknowledgedCodec.Instance.Direction);
        Assert.Equal(3, LoginAcknowledgedCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Login, DisconnectLoginCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, DisconnectLoginCodec.Instance.Direction);
        Assert.Equal(0, DisconnectLoginCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Login, LoginStartCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, LoginStartCodec.Instance.Direction);
        Assert.Equal(0, LoginStartCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Login, LoginSuccessCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, LoginSuccessCodec.Instance.Direction);
        Assert.Equal(2, LoginSuccessCodec.Instance.PacketId);
    }

    private static PacketRegistry CreateRegistry()
    {
        var registry = new PacketRegistry();
        registry.Register(LoginAcknowledgedCodec.Instance);
        registry.Register(DisconnectLoginCodec.Instance);
        registry.Register(LoginStartCodec.Instance);
        registry.Register(LoginSuccessCodec.Instance);
        return registry;
    }
}
