using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class PluginMessagePacketTests
{
    [Fact]
    public void PlayServerboundBrandRoundTrips()
    {
        // A real client encodes brand data as a String (VarInt length + bytes).
        var brandData = EncodeString("vanilla");
        var original = new PluginMessagePacket(PluginMessagePacket.BrandChannel, brandData);
        using var writer = new PacketWriterScope();

        PlayServerboundPluginMessageCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PluginMessagePacket>(ConnectionState.Play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal(PluginMessagePacket.BrandChannel, decoded.Channel);
        Assert.Equal(brandData, decoded.Data);
    }

    [Fact]
    public void ConfigurationServerboundRoundTrips()
    {
        var original = new PluginMessagePacket("minecraft:brand", [0x00, 0x01, 0x02]);
        using var writer = new PacketWriterScope();

        ServerConfigurationPluginMessageCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PluginMessagePacket>(ConnectionState.Configuration, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal("minecraft:brand", decoded.Channel);
        Assert.Equal([0x00, 0x01, 0x02], decoded.Data);
    }

    [Fact]
    public void PlayServerboundKnownWireForm()
    {
        // Packet ID 22, channel "minecraft:brand" (16 chars), data = String "vanilla".
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(22);
        writer.Writer.WriteString("minecraft:brand", PluginMessagePacket.MaxChannelLength);
        writer.Writer.WriteBytes(EncodeString("vanilla"));

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PluginMessagePacket>(ConnectionState.Play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal("minecraft:brand", decoded.Channel);
        Assert.Equal("vanilla", DecodeString(decoded.Data));
    }

    [Fact]
    public void PlayClientboundBrandRoundTrips()
    {
        var original = new PluginMessagePacket(PluginMessagePacket.BrandChannel, EncodeString("VoXelSharp"));
        using var writer = new PacketWriterScope();

        PlayClientboundPluginMessageCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PluginMessagePacket>(ConnectionState.Play, PacketDirection.Clientbound, writer.Bytes);

        Assert.Equal(PluginMessagePacket.BrandChannel, decoded.Channel);
        Assert.Equal("VoXelSharp", DecodeString(decoded.Data));
    }

    [Fact]
    public void PlayServerboundEmptyDataRoundTrips()
    {
        var original = new PluginMessagePacket("minecraft:register", []);
        using var writer = new PacketWriterScope();

        PlayServerboundPluginMessageCodec.Instance.Encode(original, writer.Writer);

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PluginMessagePacket>(ConnectionState.Play, PacketDirection.Serverbound, writer.Bytes);

        Assert.Empty(decoded.Data);
    }

    [Fact]
    public void ConfigurationServerboundKnownWireForm()
    {
        // Packet ID 2, channel "minecraft:brand", data = String "vanilla".
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(2);
        writer.Writer.WriteString("minecraft:brand", PluginMessagePacket.MaxChannelLength);
        writer.Writer.WriteBytes(EncodeString("vanilla"));

        var decoded = PacketRegistry.CreateDefault().DecodeAs<PluginMessagePacket>(ConnectionState.Configuration, PacketDirection.Serverbound, writer.Bytes);

        Assert.Equal(PluginMessagePacket.BrandChannel, decoded.Channel);
        Assert.Equal("vanilla", DecodeString(decoded.Data));
    }

    [Fact]
    public void OverlongChannelRejected()
    {
        // A channel longer than 32767 max is rejected by the String reader.
        string tooLong = new('a', PluginMessagePacket.MaxChannelLength + 1);
        Assert.Throws<ArgumentException>(() => new PluginMessagePacket(tooLong, []));
    }

    [Fact]
    public void NullArgumentsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PluginMessagePacket(null!, []));
        Assert.Throws<ArgumentNullException>(() => new PluginMessagePacket("minecraft:brand", null!));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Configuration, ServerConfigurationPluginMessageCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerConfigurationPluginMessageCodec.Instance.Direction);
        Assert.Equal(2, ServerConfigurationPluginMessageCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayServerboundPluginMessageCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, PlayServerboundPluginMessageCodec.Instance.Direction);
        Assert.Equal(22, PlayServerboundPluginMessageCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Play, PlayClientboundPluginMessageCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, PlayClientboundPluginMessageCodec.Instance.Direction);
        Assert.Equal(24, PlayClientboundPluginMessageCodec.Instance.PacketId);
    }

    private static byte[] EncodeString(string value)
    {
        var writer = new PacketWriter();
        writer.WriteString(value, PluginMessagePacket.MaxChannelLength);
        return writer.ToArray();
    }

    private static string DecodeString(byte[] value)
    {
        var reader = new PacketReader(value);
        return reader.ReadString(PluginMessagePacket.MaxChannelLength);
    }
}
