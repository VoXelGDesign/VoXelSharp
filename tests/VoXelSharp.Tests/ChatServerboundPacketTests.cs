using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class ChatServerboundPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void ServerboundChatRoundTripsWithSignature()
    {
        var signatureBytes = new byte[256];
        signatureBytes[0] = 0x42;
        var original = new ServerboundChatPacket(
            "hello world",
            1234567890,
            42,
            new ChatMessageSignature(signatureBytes),
            new LastSeenMessageUpdate(3, [0x01, 0x02, 0x03], 0x05));
        var decoded = ServerboundChatCodec.Instance.DecodeAs<ServerboundChatPacket>(
            _play, PacketDirection.Serverbound, ServerboundChatCodec.Instance.Encode(original));

        Assert.Equal("hello world", decoded.Message);
        Assert.Equal(1234567890, decoded.TimestampEpochMillis);
        Assert.Equal(42, decoded.Salt);
        Assert.NotNull(decoded.Signature);
        Assert.Equal(0x42, decoded.Signature!.Bytes[0]);
        Assert.Equal(3, decoded.LastSeenMessages.Offset);
        Assert.Equal([0x01, 0x02, 0x03], decoded.LastSeenMessages.Acknowledged);
        Assert.Equal((byte)0x05, decoded.LastSeenMessages.Checksum);
    }

    [Fact]
    public void ServerboundChatRoundTripsWithoutSignature()
    {
        var original = new ServerboundChatPacket(
            "nope", 1, 2, null, new LastSeenMessageUpdate(0, [], 0x00));
        var decoded = ServerboundChatCodec.Instance.DecodeAs<ServerboundChatPacket>(
            _play, PacketDirection.Serverbound, ServerboundChatCodec.Instance.Encode(original));

        Assert.Equal("nope", decoded.Message);
        Assert.Null(decoded.Signature);
    }

    [Fact]
    public void ServerboundChatKnownWireForm()
    {
        // Packet ID 9, message "hi", timestamp 0, salt 0, no signature, lastSeen offset 0,
        // ack 3 zero bytes, checksum 0.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(9);
        writer.Writer.WriteString("hi", 256);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteBytes([0, 0, 0]);
        writer.Writer.WriteByte(0);

        var decoded = ServerboundChatCodec.Instance.DecodeAs<ServerboundChatPacket>(
            _play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal("hi", decoded.Message);
        Assert.Null(decoded.Signature);
        Assert.Equal(0, decoded.LastSeenMessages.Offset);
    }

    [Fact]
    public void ServerboundChatTruncatedThrows()
    {
        byte[] payload = { 9, 0x02, 0x61 }; // id + partial string
        payload.AssertInvalidData(_play, PacketDirection.Serverbound);
    }

    // ---- chat_command (id 7) ----

    [Fact]
    public void ServerboundChatCommandRoundTrips()
    {
        var original = new ServerboundChatCommandPacket("say hi");
        var decoded = ServerboundChatCommandCodec.Instance.DecodeAs<ServerboundChatCommandPacket>(
            _play, PacketDirection.Serverbound, ServerboundChatCommandCodec.Instance.Encode(original));

        Assert.Equal("say hi", decoded.Command);
    }

    [Fact]
    public void ServerboundChatCommandKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(7);
        writer.Writer.WriteString("me", ServerboundChatCommandPacket.MaxCommandLength);

        var decoded = ServerboundChatCommandCodec.Instance.DecodeAs<ServerboundChatCommandPacket>(
            _play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal("me", decoded.Command);
    }

    // ---- chat_command_signed (id 8) ----

    [Fact]
    public void ServerboundChatCommandSignedRoundTrips()
    {
        var signature = new byte[256];
        signature[0] = 0x11;
        var original = new ServerboundChatCommandSignedPacket(
            "say",
            99,
            7,
            [new CommandArgumentSignature("arg", new ChatMessageSignature(signature))],
            new LastSeenMessageUpdate(1, [0xAA, 0xBB, 0xCC], 0x01));
        var decoded = ServerboundChatCommandSignedCodec.Instance.DecodeAs<ServerboundChatCommandSignedPacket>(
            _play, PacketDirection.Serverbound, ServerboundChatCommandSignedCodec.Instance.Encode(original));

        Assert.Equal("say", decoded.Command);
        Assert.Single(decoded.ArgumentSignatures);
        Assert.Equal("arg", decoded.ArgumentSignatures[0].Name);
        Assert.Equal(0x11, decoded.ArgumentSignatures[0].Signature.Bytes[0]);
        Assert.Equal(1, decoded.LastSeenMessages.Offset);
    }

    [Fact]
    public void ServerboundChatCommandSignedKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(8);
        writer.Writer.WriteString("say", ServerboundChatCommandPacket.MaxCommandLength);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteVarInt(0); // no argument signatures
        writer.Writer.WriteVarInt(0); // lastSeen offset
        writer.Writer.WriteBytes([0, 0, 0]);
        writer.Writer.WriteByte(0);

        var decoded = ServerboundChatCommandSignedCodec.Instance.DecodeAs<ServerboundChatCommandSignedPacket>(
            _play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal("say", decoded.Command);
        Assert.Empty(decoded.ArgumentSignatures);
    }

    [Fact]
    public void ServerboundChatCommandSignedRejectsTooManyArguments()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(8);
        writer.Writer.WriteString("say", ServerboundChatCommandPacket.MaxCommandLength);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteVarInt(9); // exceeds MAX_ARGUMENT_COUNT (8)
        writer.Bytes.AssertInvalidData(_play, PacketDirection.Serverbound);
    }

    // ---- chat_ack (id 6) ----

    [Fact]
    public void ServerboundChatAckRoundTrips()
    {
        var original = new ServerboundChatAckPacket(12);
        var decoded = ServerboundChatAckCodec.Instance.DecodeAs<ServerboundChatAckPacket>(
            _play, PacketDirection.Serverbound, ServerboundChatAckCodec.Instance.Encode(original));

        Assert.Equal(12, decoded.Offset);
    }

    [Fact]
    public void ServerboundChatAckKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(6);
        writer.Writer.WriteVarInt(5);

        var decoded = ServerboundChatAckCodec.Instance.DecodeAs<ServerboundChatAckPacket>(
            _play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal(5, decoded.Offset);
    }

    // ---- chat_session_update (id 10) ----

    [Fact]
    public void ServerboundChatSessionUpdateRoundTrips()
    {
        var original = new ServerboundChatSessionUpdatePacket(1, 2, 3, [0x01, 0x02], [0x03, 0x04, 0x05]);
        var decoded = ServerboundChatSessionUpdateCodec.Instance.DecodeAs<ServerboundChatSessionUpdatePacket>(
            _play, PacketDirection.Serverbound, ServerboundChatSessionUpdateCodec.Instance.Encode(original));

        Assert.Equal(1, decoded.SessionIdMsb);
        Assert.Equal(2, decoded.SessionIdLsb);
        Assert.Equal(3, decoded.ExpiresAtEpochMillis);
        Assert.Equal([0x01, 0x02], decoded.PublicKey);
        Assert.Equal([0x03, 0x04, 0x05], decoded.KeySignature);
    }

    [Fact]
    public void ServerboundChatSessionUpdateKnownWireForm()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(10);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(1);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteVarInt(2);   // public key length
        writer.Writer.WriteBytes([0xAA, 0xBB]);
        writer.Writer.WriteVarInt(0);   // key signature length (empty)

        var decoded = ServerboundChatSessionUpdateCodec.Instance.DecodeAs<ServerboundChatSessionUpdatePacket>(
            _play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal(0, decoded.SessionIdMsb);
        Assert.Equal(1, decoded.SessionIdLsb);
        Assert.Equal([0xAA, 0xBB], decoded.PublicKey);
        Assert.Empty(decoded.KeySignature);
    }

    [Fact]
    public void ServerboundChatSessionUpdateRejectsOversizedKey()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(10);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteLong(0);
        writer.Writer.WriteVarInt(ServerboundChatSessionUpdatePacket.MaxPublicKeyLength + 1);
        writer.Writer.WriteBytes(new byte[ServerboundChatSessionUpdatePacket.MaxPublicKeyLength + 1]);
        writer.Bytes.AssertInvalidData(_play, PacketDirection.Serverbound);
    }

    // ---- codec metadata ----

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, ServerboundChatCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerboundChatCodec.Instance.Direction);
        Assert.Equal(9, ServerboundChatCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ServerboundChatAckCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerboundChatAckCodec.Instance.Direction);
        Assert.Equal(6, ServerboundChatAckCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ServerboundChatCommandCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerboundChatCommandCodec.Instance.Direction);
        Assert.Equal(7, ServerboundChatCommandCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ServerboundChatCommandSignedCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerboundChatCommandSignedCodec.Instance.Direction);
        Assert.Equal(8, ServerboundChatCommandSignedCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, ServerboundChatSessionUpdateCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerboundChatSessionUpdateCodec.Instance.Direction);
        Assert.Equal(10, ServerboundChatSessionUpdateCodec.Instance.PacketId);
    }
}
