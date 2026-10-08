using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class PingRequestPacketTests
{
    // Known-good payload (packet ID + body): Timestamp 0x0123456789ABCDEF as a big-endian Long.
    private static readonly byte[] _knownGood = { 0x01, 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };

    [Fact]
    public void DecodesKnownGoodPayload()
    {
        var registry = CreateRegistry();

        var packet = Assert.IsType<PingRequestPacket>(registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, _knownGood));

        Assert.Equal(0x0123456789ABCDEF, packet.Timestamp);
    }

    [Fact]
    public void EncodesKnownGoodPayload()
    {
        using var writer = new PacketWriterScope();
        PingRequestCodec.Instance.Encode(new PingRequestPacket(0x0123456789ABCDEF), writer.Writer);

        Assert.Equal(_knownGood, writer.Bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    public void RoundTripPreservesTimestamp(long timestamp)
    {
        using var writer = new PacketWriterScope();
        PingRequestCodec.Instance.Encode(new PingRequestPacket(timestamp), writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<PingRequestPacket>(registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, writer.Bytes));

        Assert.Equal(timestamp, decoded.Timestamp);
    }

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, _knownGood.AsSpan(0, _knownGood.Length - 1)));
    }

    [Fact]
    public void TrailingBytesAreRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, [.. _knownGood, 0x00]));
    }

    [Fact]
    public void EmptyPayloadIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Serverbound, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Status, PingRequestCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PingRequestCodec.Instance.Direction);
        Assert.Equal(1, PingRequestCodec.Instance.PacketId);
    }

    private static PacketRegistry CreateRegistry()
    {
        var registry = new PacketRegistry();
        registry.Register(PingRequestCodec.Instance);
        return registry;
    }
}
