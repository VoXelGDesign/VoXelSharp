using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class HandshakePacketTests
{
    // Known-good payload (packet ID + body) hand-computed from the wire facts:
    // Protocol Version 777 = VarInt 0x89 0x06; "localhost" = VarInt(9) + 9 UTF-8 bytes;
    // Server Port 25565 = Unsigned Short big-endian 0x63 0xDD; Intent Status = VarInt 1.
    private static readonly byte[] _knownGood =
    {
        0x00, // packet ID
        0x89, 0x06, // protocol version 777
        0x09, // string length
        (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'l', (byte)'h', (byte)'o', (byte)'s', (byte)'t',
        0x63, 0xDD, // port 25565
        0x01, // intent status
    };

    [Fact]
    public void DecodesKnownGoodPayload()
    {
        var registry = CreateRegistry();
        var packet = Assert.IsType<HandshakePacket>(registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, _knownGood));

        Assert.Equal(777, packet.ProtocolVersion);
        Assert.Equal("localhost", packet.ServerAddress);
        Assert.Equal(25565, packet.ServerPort);
        Assert.Equal(HandshakeIntent.Status, packet.Intent);
    }

    [Fact]
    public void EncodesKnownGoodPayload()
    {
        var packet = new HandshakePacket(777, "localhost", 25565, HandshakeIntent.Status);

        var writer = new PacketWriter();
        HandshakeCodec.Instance.Encode(packet, writer);

        Assert.Equal(_knownGood, writer.ToArray());
    }

    [Fact]
    public void RoundTripPreservesAllFields()
    {
        var original = new HandshakePacket(-2_000_000_000, "mc.example.org", 1, HandshakeIntent.Transfer);

        var writer = new PacketWriter();
        HandshakeCodec.Instance.Encode(original, writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<HandshakePacket>(registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, writer.ToArray()));

        Assert.Equal(original.ProtocolVersion, decoded.ProtocolVersion);
        Assert.Equal(original.ServerAddress, decoded.ServerAddress);
        Assert.Equal(original.ServerPort, decoded.ServerPort);
        Assert.Equal(original.Intent, decoded.Intent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AllDocumentedIntentsDecode(int intent)
    {
        byte[] payload =
        {
            0x00, // packet ID
            0x00, // protocol version 0
            0x00, // empty address
            0x00, 0x00, // port 0
            (byte)intent,
        };

        var registry = CreateRegistry();
        var packet = Assert.IsType<HandshakePacket>(registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, payload));

        Assert.Equal((HandshakeIntent)intent, packet.Intent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void InvalidIntentIsRejected(int intent)
    {
        byte[] payload =
        {
            0x00, 0x00, 0x00, 0x00, 0x00, (byte)intent,
        };

        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, payload));
    }

    [Fact]
    public void ServerAddressOverLimitDecodeRejected()
    {
        // Address of 256 'a' exceeds the 255 code-unit limit set by the handshake codec.
        var writer = new PacketWriter();
        writer.WriteVarInt(0x00); // packet ID
        writer.WriteVarInt(1); // protocol version
        writer.WriteString(new string('a', 256), 32767); // temporarily allowed via larger limit
        writer.WriteUnsignedShort(25565);
        writer.WriteVarInt((int)HandshakeIntent.Status);

        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, writer.ToArray()));
    }

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, _knownGood.AsSpan(0, _knownGood.Length - 1)));
        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, _knownGood.AsSpan(0, 5)));
    }

    [Fact]
    public void TrailingBytesAreRejected()
    {
        var registry = CreateRegistry();

        byte[] withTrailing = [.. _knownGood, 0xAA];

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, withTrailing));
    }

    [Fact]
    public void EmptyPayloadIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Handshaking, PacketDirection.Serverbound, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void ConstructorRejectsNullAddress()
    {
        Assert.Throws<ArgumentNullException>(() => new HandshakePacket(777, null!, 25565, HandshakeIntent.Status));
    }

    [Fact]
    public void ConstructorRejectsOverlongAddress()
    {
        string address = new('a', 256);

        Assert.Throws<ArgumentException>(() => new HandshakePacket(777, address, 25565, HandshakeIntent.Status));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Handshaking, HandshakeCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, HandshakeCodec.Instance.Direction);
        Assert.Equal(0, HandshakeCodec.Instance.PacketId);
    }

    [Fact]
    public void EncodeRejectsWrongPacketType()
    {
        var writer = new PacketWriter();

        Assert.Throws<ArgumentException>(() => HandshakeCodec.Instance.Encode(new StatusRequestPacket(), writer));
    }

    private static PacketRegistry CreateRegistry()
    {
        var registry = new PacketRegistry();
        registry.Register(HandshakeCodec.Instance);
        return registry;
    }
}
