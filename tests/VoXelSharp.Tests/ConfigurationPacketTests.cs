using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Nbt;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class ConfigurationPacketTests
{
    private const ConnectionState _config = ConnectionState.Configuration;

    // ---- Finish Configuration (clientbound, no fields) ----

    [Fact]
    public void FinishConfigurationRoundTrips()
    {
        byte[] payload = FinishConfigurationCodec.Instance.Encode(new FinishConfigurationPacket());
        _ = FinishConfigurationCodec.Instance.DecodeAs<FinishConfigurationPacket>(_config, PacketDirection.Clientbound, payload);
    }

    [Fact]
    public void FinishConfigurationKnownWireForm()
    {
        // Payload: packet ID 3 (0x03), no fields.
        byte[] payload = { 0x03 };
        _ = FinishConfigurationCodec.Instance.DecodeAs<FinishConfigurationPacket>(_config, PacketDirection.Clientbound, payload);
    }

    [Fact]
    public void FinishConfigurationTrailingBytes()
    {
        byte[] payload = { 0x03, 0xAA };
        payload.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    // ---- Acknowledge Finish Configuration (serverbound, no fields) ----

    [Fact]
    public void AcknowledgeFinishConfigurationRoundTrips()
    {
        byte[] payload = AcknowledgeFinishConfigurationCodec.Instance.Encode(new AcknowledgeFinishConfigurationPacket());
        _ = AcknowledgeFinishConfigurationCodec.Instance.DecodeAs<AcknowledgeFinishConfigurationPacket>(_config, PacketDirection.Serverbound, payload);
    }

    [Fact]
    public void AcknowledgeFinishConfigurationKnownWireForm()
    {
        // Payload: packet ID 3 (0x03), no fields.
        byte[] payload = { 0x03 };
        _ = AcknowledgeFinishConfigurationCodec.Instance.DecodeAs<AcknowledgeFinishConfigurationPacket>(_config, PacketDirection.Serverbound, payload);
    }

    [Fact]
    public void AcknowledgeFinishConfigurationTrailingBytes()
    {
        byte[] payload = { 0x03, 0xBB };
        payload.AssertInvalidData(_config, PacketDirection.Serverbound);
    }

    // ---- Feature Flags (clientbound) ----

    [Fact]
    public void FeatureFlagsRoundTrips()
    {
        var original = new FeatureFlagsPacket(["minecraft:vanilla"]);
        var decoded = FeatureFlagsCodec.Instance.DecodeAs<FeatureFlagsPacket>(
            _config, PacketDirection.Clientbound, FeatureFlagsCodec.Instance.Encode(original));
        Assert.Equal(["minecraft:vanilla"], decoded.Features);
    }

    [Fact]
    public void FeatureFlagsKnownWireForm()
    {
        // Packet ID 13 (0x0D), then count 1 and the identifier "minecraft:vanilla".
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(13);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:vanilla", FeatureFlagsPacket.MaxIdentifierLength);

        var decoded = FeatureFlagsCodec.Instance.DecodeAs<FeatureFlagsPacket>(_config, PacketDirection.Clientbound, writer.Bytes);
        Assert.Equal("minecraft:vanilla", decoded.Features[0]);
    }

    [Fact]
    public void FeatureFlagsEmpty()
    {
        byte[] payload = FeatureFlagsCodec.Instance.Encode(new FeatureFlagsPacket([]));
        var decoded = FeatureFlagsCodec.Instance.DecodeAs<FeatureFlagsPacket>(_config, PacketDirection.Clientbound, payload);
        Assert.Empty(decoded.Features);
    }

    [Fact]
    public void FeatureFlagsNegativeCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(13); // packet ID
        writer.Writer.WriteVarInt(-1); // negative feature count
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    [Fact]
    public void FeatureFlagsTrailingBytes()
    {
        using var writer = new PacketWriterScope();
        FeatureFlagsCodec.Instance.Encode(new FeatureFlagsPacket([]), writer.Writer);
        writer.Writer.WriteByte(0x01); // trailing garbage
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    // ---- Known Packs (clientbound, id 15) ----

    [Fact]
    public void KnownPacksRoundTrips()
    {
        var original = new KnownPacksPacket([new KnownPack("minecraft", "core", "26.3")]);
        var decoded = KnownPacksCodec.Instance.DecodeAs<KnownPacksPacket>(
            _config, PacketDirection.Clientbound, KnownPacksCodec.Instance.Encode(original));

        Assert.Single(decoded.Packs);
        Assert.Equal("minecraft", decoded.Packs[0].Namespace);
        Assert.Equal("core", decoded.Packs[0].ID);
        Assert.Equal("26.3", decoded.Packs[0].Version);
    }

    [Fact]
    public void KnownPacksKnownWireForm()
    {
        // Packet ID 15 (0x0F), count 1, then {namespace "minecraft", id "core", version "26.3"}.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(15);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft", KnownPacksPacket.MaxPackFieldLength);
        writer.Writer.WriteString("core", KnownPacksPacket.MaxPackFieldLength);
        writer.Writer.WriteString("26.3", KnownPacksPacket.MaxPackFieldLength);

        var decoded = KnownPacksCodec.Instance.DecodeAs<KnownPacksPacket>(_config, PacketDirection.Clientbound, writer.Bytes);
        Assert.Equal("minecraft", decoded.Packs[0].Namespace);
        Assert.Equal("core", decoded.Packs[0].ID);
        Assert.Equal("26.3", decoded.Packs[0].Version);
    }

    [Fact]
    public void KnownPacksNegativeCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(15); // packet ID
        writer.Writer.WriteVarInt(-1); // negative pack count
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    [Fact]
    public void KnownPacksTrailingBytes()
    {
        using var writer = new PacketWriterScope();
        KnownPacksCodec.Instance.Encode(new KnownPacksPacket([]), writer.Writer);
        writer.Writer.WriteByte(0x00); // trailing garbage
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    // ---- Known Packs (serverbound, id 7) ----

    [Fact]
    public void ServerKnownPacksRoundTrips()
    {
        var original = new KnownPacksPacket([new KnownPack("minecraft", "core", "26.3")]);
        var decoded = ServerKnownPacksCodec.Instance.DecodeAs<KnownPacksPacket>(
            _config, PacketDirection.Serverbound, ServerKnownPacksCodec.Instance.Encode(original));

        Assert.Single(decoded.Packs);
        Assert.Equal("minecraft", decoded.Packs[0].Namespace);
        Assert.Equal("core", decoded.Packs[0].ID);
        Assert.Equal("26.3", decoded.Packs[0].Version);
    }

    [Fact]
    public void ServerKnownPacksKnownWireForm()
    {
        // Packet ID 7 (0x07), count 1, then {namespace "minecraft", id "core", version "26.3"}.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(7);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft", KnownPacksPacket.MaxPackFieldLength);
        writer.Writer.WriteString("core", KnownPacksPacket.MaxPackFieldLength);
        writer.Writer.WriteString("26.3", KnownPacksPacket.MaxPackFieldLength);

        var decoded = ServerKnownPacksCodec.Instance.DecodeAs<KnownPacksPacket>(_config, PacketDirection.Serverbound, writer.Bytes);
        Assert.Equal("minecraft", decoded.Packs[0].Namespace);
        Assert.Equal("core", decoded.Packs[0].ID);
        Assert.Equal("26.3", decoded.Packs[0].Version);
    }

    [Fact]
    public void ServerKnownPacksNegativeCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(7);  // packet ID
        writer.Writer.WriteVarInt(-1); // negative pack count
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Serverbound);
    }

    // ---- Registry Data (clientbound) ----

    [Fact]
    public void RegistryDataRoundTripsWithOmittedData()
    {
        var original = RegistryDataPacket.Create("minecraft:dimension_type", "minecraft:overworld");
        var decoded = RegistryDataCodec.Instance.DecodeAs<RegistryDataPacket>(
            _config, PacketDirection.Clientbound, RegistryDataCodec.Instance.Encode(original));

        Assert.Equal("minecraft:dimension_type", decoded.RegistryId);
        Assert.Single(decoded.Entries);
        Assert.Equal("minecraft:overworld", decoded.Entries[0].EntryId);
        Assert.Null(decoded.Entries[0].Data);
    }

    [Fact]
    public void RegistryDataRoundTripsWithNbtData()
    {
        var data = new NbtCompound([new NbtNamedTag("fixed_time", new NbtLong(6000))]);
        var original = new RegistryDataPacket(
            "minecraft:dimension_type",
            [new RegistryDataEntry("minecraft:overworld", data)]);
        var decoded = RegistryDataCodec.Instance.DecodeAs<RegistryDataPacket>(
            _config, PacketDirection.Clientbound, RegistryDataCodec.Instance.Encode(original));

        Assert.Equal("minecraft:overworld", decoded.Entries[0].EntryId);
        Assert.NotNull(decoded.Entries[0].Data);
        Assert.Equal(NbtTagType.CompoundTag, decoded.Entries[0].Data!.TagType);
    }

    [Fact]
    public void RegistryDataKnownWireFormWithOmittedData()
    {
        // Packet ID 7 (0x07), registry "minecraft:dimension_type", count 1,
        // entry "minecraft:overworld", Data omitted (Prefixed Optional NBT = false).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(7);
        writer.Writer.WriteString("minecraft:dimension_type", RegistryDataPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:overworld", RegistryDataPacket.MaxIdentifierLength);
        writer.Writer.WriteBoolean(false);

        var decoded = RegistryDataCodec.Instance.DecodeAs<RegistryDataPacket>(_config, PacketDirection.Clientbound, writer.Bytes);
        Assert.Equal("minecraft:dimension_type", decoded.RegistryId);
        Assert.Single(decoded.Entries);
        Assert.Equal("minecraft:overworld", decoded.Entries[0].EntryId);
        Assert.Null(decoded.Entries[0].Data);
    }

    [Fact]
    public void RegistryDataTruncatedNbtThrows()
    {
        // Entry declares Data present (true) but the NBT payload is truncated.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(7);
        writer.Writer.WriteString("minecraft:dimension_type", RegistryDataPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:overworld", RegistryDataPacket.MaxIdentifierLength);
        writer.Writer.WriteBoolean(true); // data present
        writer.Writer.WriteByte(0x0A);    // only the root compound type byte
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    [Fact]
    public void RegistryDataNegativeEntryCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(7);
        writer.Writer.WriteString("minecraft:dimension_type", RegistryDataPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(-1); // negative entry count
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    [Fact]
    public void RegistryDataTrailingBytes()
    {
        using var writer = new PacketWriterScope();
        RegistryDataCodec.Instance.Encode(RegistryDataPacket.Create("minecraft:dimension_type"), writer.Writer);
        writer.Writer.WriteByte(0x00); // trailing garbage
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    // ---- Update Tags (clientbound) ----

    [Fact]
    public void UpdateTagsRoundTrips()
    {
        var original = new UpdateTagsPacket(
        [
            new TaggedRegistry("minecraft:block", [RegistryTag.With("minecraft:climbable", 1, 2, 3)]),
        ]);
        var decoded = UpdateTagsCodec.Instance.DecodeAs<UpdateTagsPacket>(
            _config, PacketDirection.Clientbound, UpdateTagsCodec.Instance.Encode(original));

        Assert.Single(decoded.TaggedRegistries);
        Assert.Equal("minecraft:block", decoded.TaggedRegistries[0].Registry);
        Assert.Single(decoded.TaggedRegistries[0].Tags);
        Assert.Equal("minecraft:climbable", decoded.TaggedRegistries[0].Tags[0].Name);
        Assert.Equal([1, 2, 3], decoded.TaggedRegistries[0].Tags[0].EntryIds);
    }

    [Fact]
    public void UpdateTagsKnownWireForm()
    {
        // Packet ID 14 (0x0E), 1 tagged registry "minecraft:block" with 1 tag
        // "minecraft:climbable" whose entries are the VarInts 1, 2, 3.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(14); // packet ID
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:block", UpdateTagsPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:climbable", UpdateTagsPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(3);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteVarInt(2);
        writer.Writer.WriteVarInt(3);

        var decoded = UpdateTagsCodec.Instance.DecodeAs<UpdateTagsPacket>(_config, PacketDirection.Clientbound, writer.Bytes);
        Assert.Equal("minecraft:block", decoded.TaggedRegistries[0].Registry);
        Assert.Equal("minecraft:climbable", decoded.TaggedRegistries[0].Tags[0].Name);
        Assert.Equal([1, 2, 3], decoded.TaggedRegistries[0].Tags[0].EntryIds);
    }

    [Fact]
    public void UpdateTagsNegativeRegistryCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(14); // packet ID
        writer.Writer.WriteVarInt(-1); // negative tagged-registry count
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    [Fact]
    public void UpdateTagsNegativeTagCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(14);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:block", UpdateTagsPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(-1); // negative tag count
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    [Fact]
    public void UpdateTagsNegativeEntryCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(14);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:block", UpdateTagsPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(1);
        writer.Writer.WriteString("minecraft:climbable", UpdateTagsPacket.MaxIdentifierLength);
        writer.Writer.WriteVarInt(-1); // negative entry count
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    [Fact]
    public void UpdateTagsTrailingBytes()
    {
        using var writer = new PacketWriterScope();
        UpdateTagsCodec.Instance.Encode(new UpdateTagsPacket([]), writer.Writer);
        writer.Writer.WriteByte(0x00); // trailing garbage
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Clientbound);
    }

    // ---- Client Information (serverbound) ----

    [Fact]
    public void ClientInformationRoundTrips()
    {
        var original = new ClientInformationPacket(
            "en_US", 8, ClientChatMode.Enabled, true, 0x7F, ClientMainHand.Right, false, true, ClientParticleStatus.All);
        var decoded = ClientInformationCodec.Instance.DecodeAs<ClientInformationPacket>(
            _config, PacketDirection.Serverbound, ClientInformationCodec.Instance.Encode(original));

        Assert.Equal("en_US", decoded.Locale);
        Assert.Equal(8, decoded.ViewDistance);
        Assert.Equal(ClientChatMode.Enabled, decoded.ChatMode);
        Assert.True(decoded.ChatColors);
        Assert.Equal(0x7F, decoded.DisplayedSkinParts);
        Assert.Equal(ClientMainHand.Right, decoded.MainHand);
        Assert.False(decoded.EnableTextFiltering);
        Assert.True(decoded.AllowServerListings);
        Assert.Equal(ClientParticleStatus.All, decoded.ParticleStatus);
    }

    [Fact]
    public void ClientInformationKnownWireForm()
    {
        // Packet ID 0, locale "en_US", view distance 8, chat mode 0 (enabled),
        // chat colors true, skin parts 0x7F, main hand 1 (right), text filtering false,
        // allow listings true, particle status 0 (all).
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteString("en_US", ClientInformationPacket.MaxLocaleLength);
        writer.Writer.WriteByte(8);
        writer.Writer.WriteVarInt(0); // chat mode enabled
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteByte(0x7F);
        writer.Writer.WriteVarInt(1); // main hand right
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteVarInt(0); // particle status all

        var decoded = ClientInformationCodec.Instance.DecodeAs<ClientInformationPacket>(_config, PacketDirection.Serverbound, writer.Bytes);
        Assert.Equal("en_US", decoded.Locale);
        Assert.Equal(8, decoded.ViewDistance);
        Assert.Equal(ClientChatMode.Enabled, decoded.ChatMode);
        Assert.True(decoded.ChatColors);
        Assert.Equal(0x7F, decoded.DisplayedSkinParts);
        Assert.Equal(ClientMainHand.Right, decoded.MainHand);
        Assert.False(decoded.EnableTextFiltering);
        Assert.True(decoded.AllowServerListings);
        Assert.Equal(ClientParticleStatus.All, decoded.ParticleStatus);
    }

    [Fact]
    public void ClientInformationInvalidChatModeThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteString("en_US", ClientInformationPacket.MaxLocaleLength);
        writer.Writer.WriteByte(8);
        writer.Writer.WriteVarInt(3); // invalid chat mode
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Serverbound);
    }

    [Fact]
    public void ClientInformationInvalidMainHandThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteString("en_US", ClientInformationPacket.MaxLocaleLength);
        writer.Writer.WriteByte(8);
        writer.Writer.WriteVarInt(0); // chat mode enabled
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteByte(0x00);
        writer.Writer.WriteVarInt(2); // invalid main hand
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Serverbound);
    }

    [Fact]
    public void ClientInformationInvalidParticleStatusThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteString("en_US", ClientInformationPacket.MaxLocaleLength);
        writer.Writer.WriteByte(8);
        writer.Writer.WriteVarInt(0); // chat mode enabled
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteByte(0x00);
        writer.Writer.WriteVarInt(1);  // main hand right
        writer.Writer.WriteBoolean(false);
        writer.Writer.WriteBoolean(true);
        writer.Writer.WriteVarInt(3); // invalid particle status
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Serverbound);
    }

    [Fact]
    public void ClientInformationTruncatedThrows()
    {
        // Only the packet ID and locale.
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(0);
        writer.Writer.WriteString("en", ClientInformationPacket.MaxLocaleLength);
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Serverbound);
    }

    [Fact]
    public void ClientInformationTrailingBytes()
    {
        using var writer = new PacketWriterScope();
        ClientInformationCodec.Instance.Encode(
            new ClientInformationPacket("en_US", 8, ClientChatMode.Enabled, true, 0x00, ClientMainHand.Right, false, true, ClientParticleStatus.All),
            writer.Writer);
        writer.Writer.WriteByte(0x00); // trailing garbage
        writer.Bytes.AssertInvalidData(_config, PacketDirection.Serverbound);
    }

    // ---- Codec metadata ----

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Configuration, ClientInformationCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ClientInformationCodec.Instance.Direction);
        Assert.Equal(0, ClientInformationCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Configuration, ServerKnownPacksCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, ServerKnownPacksCodec.Instance.Direction);
        Assert.Equal(7, ServerKnownPacksCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Configuration, KnownPacksCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, KnownPacksCodec.Instance.Direction);
        Assert.Equal(15, KnownPacksCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Configuration, RegistryDataCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, RegistryDataCodec.Instance.Direction);
        Assert.Equal(7, RegistryDataCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Configuration, UpdateTagsCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, UpdateTagsCodec.Instance.Direction);
        Assert.Equal(14, UpdateTagsCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Configuration, FeatureFlagsCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, FeatureFlagsCodec.Instance.Direction);
        Assert.Equal(13, FeatureFlagsCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Configuration, FinishConfigurationCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, FinishConfigurationCodec.Instance.Direction);
        Assert.Equal(3, FinishConfigurationCodec.Instance.PacketId);

        Assert.Equal(ConnectionState.Configuration, AcknowledgeFinishConfigurationCodec.Instance.State);
        Assert.Equal(PacketDirection.Serverbound, AcknowledgeFinishConfigurationCodec.Instance.Direction);
        Assert.Equal(3, AcknowledgeFinishConfigurationCodec.Instance.PacketId);
    }
}
