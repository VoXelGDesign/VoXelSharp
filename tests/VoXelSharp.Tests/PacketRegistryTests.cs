using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class PacketRegistryTests
{
    [Fact]
    public void DispatchIsKeyedByStateDirectionAndPacketId()
    {
        var registry = CreateDefaultRegistry();

        IPacketCodec codec = registry.Get(ConnectionState.Handshaking, PacketDirection.Serverbound, 0);
        Assert.IsType<HandshakeCodec>(codec);

        // The same numeric packet ID in Status/Clientbound resolves to a different codec.
        codec = registry.Get(ConnectionState.Status, PacketDirection.Clientbound, 0);
        Assert.IsType<StatusResponseCodec>(codec);

        // And a different direction in Status resolves differently again.
        codec = registry.Get(ConnectionState.Status, PacketDirection.Serverbound, 1);
        Assert.IsType<PingRequestCodec>(codec);
        codec = registry.Get(ConnectionState.Status, PacketDirection.Clientbound, 1);
        Assert.IsType<PongResponseCodec>(codec);
    }

    [Fact]
    public void RegisteringDuplicateIdentityThrows()
    {
        var registry = new PacketRegistry();
        registry.Register(HandshakeCodec.Instance);

        Assert.Throws<InvalidOperationException>(() => registry.Register(HandshakeCodec.Instance));
    }

    [Fact]
    public void UnknownPacketIdThrowsInvalidData()
    {
        var registry = CreateDefaultRegistry();

        // No serverbound ID 2 exists in Status for this registry.
        Assert.Throws<InvalidDataException>(() => registry.Get(ConnectionState.Status, PacketDirection.Serverbound, 2));
    }

    [Fact]
    public void WrongStateForPacketIdThrowsInvalidData()
    {
        var registry = CreateDefaultRegistry();

        // ID 0 exists in Handshaking/Serverbound and Status/Serverbound, but not in Play.
        Assert.Throws<InvalidDataException>(() => registry.Get(ConnectionState.Play, PacketDirection.Serverbound, 0));
    }

    [Fact]
    public void WrongDirectionForPacketIdThrowsInvalidData()
    {
        var registry = CreateDefaultRegistry();

        // ID 0 in Handshaking only exists serverbound.
        Assert.Throws<InvalidDataException>(() => registry.Get(ConnectionState.Handshaking, PacketDirection.Clientbound, 0));
    }

    [Fact]
    public void TryGetReturnsFalseForUnknownIdentity()
    {
        var registry = CreateDefaultRegistry();

        Assert.False(registry.TryGet(ConnectionState.Play, PacketDirection.Serverbound, 0, out _));
        Assert.False(registry.TryGet(ConnectionState.Handshaking, PacketDirection.Clientbound, 0, out _));
    }

    [Fact]
    public void DecodeRejectsWrongDirectionPacket()
    {
        // Encode a Handshake (serverbound) then try to decode it as clientbound: identity mismatch.
        using var writer = new PacketWriterScope();
        HandshakeCodec.Instance.Encode(new HandshakePacket(777, "x", 25565, HandshakeIntent.Status), writer.Writer);

        var registry = CreateDefaultRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Handshaking, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void DecodeRejectsPacketFromWrongState()
    {
        // Handshake bytes must not decode in the Status state.
        using var writer = new PacketWriterScope();
        HandshakeCodec.Instance.Encode(new HandshakePacket(777, "x", 25565, HandshakeIntent.Status), writer.Writer);

        var registry = CreateDefaultRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, writer.Bytes));
    }

    [Fact]
    public void RegisterRejectsNull()
    {
        var registry = new PacketRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    [Fact]
    public void GetThrowsWithoutCodecRegistered()
    {
        var registry = new PacketRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Get(ConnectionState.Handshaking, PacketDirection.Serverbound, 0));
    }

    private static PacketRegistry CreateDefaultRegistry()
    {
        var registry = new PacketRegistry();
        registry.Register(HandshakeCodec.Instance);
        registry.Register(StatusRequestCodec.Instance);
        registry.Register(StatusResponseCodec.Instance);
        registry.Register(PingRequestCodec.Instance);
        registry.Register(PongResponseCodec.Instance);
        registry.Register(LoginAcknowledgedCodec.Instance);
        registry.Register(DisconnectLoginCodec.Instance);
        registry.Register(LoginStartCodec.Instance);
        registry.Register(LoginSuccessCodec.Instance);
        return registry;
    }
}
