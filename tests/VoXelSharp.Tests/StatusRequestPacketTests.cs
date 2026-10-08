using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class StatusRequestPacketTests
{
    private static readonly byte[] _knownGood = { 0x00 }; // packet ID only, no fields

    [Fact]
    public void DecodesKnownGoodPayload()
    {
        var registry = CreateRegistry();

        var packet = Assert.IsType<StatusRequestPacket>(registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, _knownGood));
        Assert.NotNull(packet);
    }

    [Fact]
    public void EncodesKnownGoodPayload()
    {
        using var writer = new PacketWriterScope();

        StatusRequestCodec.Instance.Encode(new StatusRequestPacket(), writer.Writer);

        Assert.Equal(_knownGood, writer.Bytes);
    }

    [Fact]
    public void RoundTripProducesSamePacket()
    {
        using var writer = new PacketWriterScope();
        StatusRequestCodec.Instance.Encode(new StatusRequestPacket(), writer.Writer);

        var registry = CreateRegistry();
        var decoded = registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, writer.Bytes);

        Assert.IsType<StatusRequestPacket>(decoded);
    }

    [Fact]
    public void EmptyPayloadIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void TrailingBytesAreRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, new byte[] { 0x00, 0x01 }));
    }

    [Fact]
    public void UnknownPacketIdIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, new byte[] { 0x02 }));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Status, StatusRequestCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, StatusRequestCodec.Instance.Direction);
        Assert.Equal(0, StatusRequestCodec.Instance.PacketId);
    }

    private static PacketRegistry CreateRegistry()
    {
        var registry = new PacketRegistry();
        registry.Register(StatusRequestCodec.Instance);
        return registry;
    }
}
