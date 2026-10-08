using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class StatusResponsePacketTests
{
    // Known-good payload (packet ID + body): JSON "{\"description\":{\"text\":\"Hello\"}}" is 32
    // bytes; String writes VarInt(32)=0x20 followed by the UTF-8 bytes.
    private const string _sampleJson = """{"description":{"text":"Hello"}}""";

    private static readonly byte[] _knownGoodBody =
    {
        0x20,
        0x7B, 0x22, 0x64, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E, 0x22, 0x3A,
        0x7B, 0x22, 0x74, 0x65, 0x78, 0x74, 0x22, 0x3A, 0x22, 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x22,
        0x7D, 0x7D,
    };

    private static byte[] KnownGood => [0x00, .. _knownGoodBody];

    [Fact]
    public void DecodesKnownGoodPayload()
    {
        var registry = CreateRegistry();

        var packet = Assert.IsType<StatusResponsePacket>(registry.Decode(ConnectionState.Status, PacketDirection.Clientbound, KnownGood));

        Assert.Equal(_sampleJson, packet.Json);
    }

    [Fact]
    public void EncodesKnownGoodPayload()
    {
        using var writer = new PacketWriterScope();

        StatusResponseCodec.Instance.Encode(new StatusResponsePacket(_sampleJson), writer.Writer);

        Assert.Equal(KnownGood, writer.Bytes);
    }

    [Fact]
    public void RoundTripPreservesJson()
    {
        const string json = """{"version":{"name":"26.3","protocol":777},"players":{"max":20,"online":0}}""";
        using var writer = new PacketWriterScope();
        StatusResponseCodec.Instance.Encode(new StatusResponsePacket(json), writer.Writer);

        var registry = CreateRegistry();
        var decoded = Assert.IsType<StatusResponsePacket>(registry.Decode(ConnectionState.Status, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(json, decoded.Json);
    }

    [Fact]
    public void JsonOverLimitRejectedOnDecode()
    {
        // 32768 code units exceeds the 32767 limit; encode with a larger write limit so the
        // decode-side enforcement (ReadString maxLength 32767) is what rejects it.
        var writer = new PacketWriter();
        writer.WriteVarInt(0x00);
        writer.WriteString(new string('a', 32768), 65535);

        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Clientbound, writer.ToArray()));
    }

    [Fact]
    public void ConstructorRejectsNullJson()
    {
        Assert.Throws<ArgumentNullException>(() => new StatusResponsePacket(null!));
    }

    [Fact]
    public void ConstructorRejectsOverlongJson()
    {
        Assert.Throws<ArgumentException>(() => new StatusResponsePacket(new string('a', StatusResponsePacket.MaxJsonLength + 1)));
    }

    [Fact]
    public void TruncatedPayloadIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Clientbound, KnownGood.AsSpan(0, KnownGood.Length - 1)));
    }

    [Fact]
    public void InvalidUtf8InJsonIsRejected()
    {
        // String length 2 but body bytes are not valid UTF-8 (0xC3 must be a start byte of a
        // multi-byte sequence, and 0x28 is not a continuation byte).
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Clientbound, new byte[] { 0x00, 0x02, 0xC3, 0x28 }));
    }

    [Fact]
    public void EmptyPayloadIsRejected()
    {
        var registry = CreateRegistry();

        Assert.Throws<InvalidDataException>(() => registry.Decode(ConnectionState.Status, PacketDirection.Clientbound, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Status, StatusResponseCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, StatusResponseCodec.Instance.Direction);
        Assert.Equal(0, StatusResponseCodec.Instance.PacketId);
    }

    private static PacketRegistry CreateRegistry()
    {
        var registry = new PacketRegistry();
        registry.Register(StatusResponseCodec.Instance);
        return registry;
    }
}
