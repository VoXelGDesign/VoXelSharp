using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Nbt;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class SystemChatPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    [Fact]
    public void StringContentRoundTrips()
    {
        var original = new SystemChatPacket(new NbtString("Hello, world!"), overlay: false);
        using var writer = new PacketWriterScope();

        SystemChatCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<SystemChatPacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal("Hello, world!", Assert.IsType<NbtString>(decoded.Content).Value);
        Assert.False(decoded.Overlay);
    }

    [Fact]
    public void CompoundContentRoundTrips()
    {
        var compound = new NbtCompound(
        [
            new NbtNamedTag("text", new NbtString("Bold hello")),
            new NbtNamedTag("bold", new NbtByte(1)),
        ]);
        var original = new SystemChatPacket(compound, overlay: true);
        using var writer = new PacketWriterScope();

        SystemChatCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<SystemChatPacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        var content = Assert.IsType<NbtCompound>(decoded.Content);
        Assert.Equal("Bold hello", Assert.IsType<NbtString>(content.Tags[0].Value).Value);
        Assert.Equal(1, Assert.IsType<NbtByte>(content.Tags[1].Value).Value);
        Assert.True(decoded.Overlay);
    }

    [Fact]
    public void KnownWireForm()
    {
        // Packet ID 124 (VarInt 0x7C), Text Component: NBT String root "Hello" (0x08,
        // unsigned-short length 5, ASCII bytes), Overlay = false (0x00).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(124);
        writer.Writer.WriteByte(0x08);
        writer.Writer.WriteShort(5);
        foreach (byte b in "Hello"u8)
        {
            writer.Writer.WriteByte(b);
        }

        writer.Writer.WriteBoolean(false);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<SystemChatPacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal("Hello", Assert.IsType<NbtString>(decoded.Content).Value);
        Assert.False(decoded.Overlay);
    }

    [Fact]
    public void OverlayTrueRoundTrips()
    {
        var original = new SystemChatPacket(new NbtString("action"), overlay: true);
        using var writer = new PacketWriterScope();

        SystemChatCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<SystemChatPacket>(_play, PacketDirection.Clientbound, writer.Bytes);

        Assert.True(decoded.Overlay);
    }

    [Fact]
    public void TruncatedContentThrows()
    {
        // Packet ID 124, String root declaring length 5 but no payload bytes.
        byte[] payload = [124, 0x08, 0x00, 0x05];

        payload.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void MissingOverlayThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(124);
        writer.Writer.WriteByte(0x08);
        writer.Writer.WriteShort(0); // empty string

        writer.Bytes.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void EndRootRejected()
    {
        // TAG_End (0x00) as the Text Component root type byte is not a valid root.
        byte[] payload = [124, 0x00];

        payload.AssertInvalidData(_play, PacketDirection.Clientbound);
    }

    [Fact]
    public void NullContentRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemChatPacket(null!, false));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, SystemChatCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, SystemChatCodec.Instance.Direction);
        Assert.Equal(124, SystemChatCodec.Instance.PacketId);
    }
}
