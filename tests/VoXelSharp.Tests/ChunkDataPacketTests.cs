using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Nbt;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class ChunkDataStructureTests
{
    [Theory]
    [InlineData(256, 9)]
    [InlineData(384, 9)]
    [InlineData(128, 8)]
    [InlineData(64, 7)]
    [InlineData(32, 6)]
    [InlineData(1, 1)]
    public void HeightmapBitsPerEntryMatchesCeilLog2(int worldHeight, int expected)
        => Assert.Equal(expected, ChunkDataIO.HeightmapBitsPerEntry(worldHeight));

    [Fact]
    public void EncodeHeightmapPacks256Entries()
    {
        var heights = new int[256];
        for (int i = 0; i < heights.Length; i++)
        {
            heights[i] = i; // 0..255 fits in 9 bits
        }

        long[] data = ChunkDataIO.EncodeHeightmap(384, heights);

        Assert.Equal(DataArrayPacker.LongCount(256, 9), data.Length);
    }

    [Fact]
    public void EncodeHeightmapRejectsWrongEntryCount()
    {
        Assert.Throws<ArgumentException>(() => ChunkDataIO.EncodeHeightmap(384, new int[255]));
    }

    [Fact]
    public void ChunkSectionsRoundTrip()
    {
        // One section: 4096 stone blocks (BPE 0 single-valued), 64 plains biomes.
        var blocks = new PalettedContainer(0, [1], ChunkDataIO.SectionEntryCount, []);
        var biomes = new PalettedContainer(0, [0], ChunkDataIO.BiomeEntryCount, []);
        var section = new ChunkSection(4096, 0, blocks, biomes);

        byte[] payload = ChunkDataIO.EncodeSections([section]);
        ChunkSection[] decoded = ChunkDataIO.DecodeSections(payload, 1);

        Assert.Single(decoded);
        Assert.Equal(4096, decoded[0].BlockCount);
        Assert.Equal(0, decoded[0].FluidCount);
        Assert.Equal(0, decoded[0].Blocks.BitsPerEntry);
        Assert.Equal(1, decoded[0].Blocks.Palette[0]);
        Assert.Equal(0, decoded[0].Biomes.Palette[0]);
    }

    [Fact]
    public void ChunkSectionsIndirectAndDirectRoundTrip()
    {
        var indirectIndices = new int[ChunkDataIO.SectionEntryCount];
        for (int i = 0; i < indirectIndices.Length; i++)
        {
            indirectIndices[i] = i % 3;
        }

        var blocks = new PalettedContainer(4, [1, 2, 3], ChunkDataIO.SectionEntryCount, indirectIndices);

        var directGlobals = new int[ChunkDataIO.BiomeEntryCount];
        for (int i = 0; i < directGlobals.Length; i++)
        {
            directGlobals[i] = i;
        }

        var biomes = new PalettedContainer(PalettedContainer.DirectBiomeBitsPerEntry, [], ChunkDataIO.BiomeEntryCount, directGlobals);

        byte[] payload = ChunkDataIO.EncodeSections([new ChunkSection(100, 2, blocks, biomes)]);
        ChunkSection[] decoded = ChunkDataIO.DecodeSections(payload, 1);

        Assert.Equal(100, decoded[0].BlockCount);
        Assert.Equal(2, decoded[0].FluidCount);
        Assert.Equal(4, decoded[0].Blocks.BitsPerEntry);
        Assert.Equal(PalettedContainer.DirectBiomeBitsPerEntry, decoded[0].Biomes.BitsPerEntry);
        Assert.Equal(1, decoded[0].Blocks.Entries[1]);
        Assert.Equal(5, decoded[0].Biomes.Entries[5]);
    }

    [Fact]
    public void DecodeSectionsTruncatedThrows()
    {
        bool caught = false;
        try
        {
            ChunkDataIO.DecodeSections(new byte[] { 0x00, 0x01 }, 1); // only 2 bytes
        }
        catch (InvalidDataException)
        {
            caught = true;
        }

        Assert.True(caught);
    }

    [Fact]
    public void DecodeSectionsTrailingBytesThrows()
    {
        var blocks = new PalettedContainer(0, [0], ChunkDataIO.SectionEntryCount, []);
        var biomes = new PalettedContainer(0, [0], ChunkDataIO.BiomeEntryCount, []);
        byte[] payload = ChunkDataIO.EncodeSections([new ChunkSection(0, 0, blocks, biomes)]);
        byte[] padded = [.. payload, 0xAA];

        Assert.Throws<InvalidDataException>(() => ChunkDataIO.DecodeSections(padded, 1));
    }

    [Fact]
    public void DecodeSectionsNegativeCountThrows()
    {
        Assert.Throws<InvalidDataException>(() => ChunkDataIO.DecodeSections([], -1));
    }
}

public class ChunkDataPacketTests
{
    private const ConnectionState _play = ConnectionState.Play;

    private static ChunkDataPacket CreateMinimalPacket()
    {
        var blocks = new PalettedContainer(0, [1], ChunkDataIO.SectionEntryCount, []);
        var biomes = new PalettedContainer(0, [0], ChunkDataIO.BiomeEntryCount, []);
        byte[] sections = ChunkDataIO.EncodeSections(
        [
            new ChunkSection(4096, 0, blocks, biomes),
        ]);
        long[] heightmap = ChunkDataIO.EncodeHeightmap(384, new int[256]);

        return new ChunkDataPacket(
            chunkX: 0,
            chunkZ: 0,
            heightmaps: [new Heightmap(4, heightmap)],
            data: sections,
            blockEntities: [],
            light: new LightData([], [], [], [], [], []));
    }

    [Fact]
    public void ChunkDataRoundTrips()
    {
        var original = CreateMinimalPacket();
        using var writer = new PacketWriterScope();

        ChunkDataCodec.Instance.Encode(original, writer.Writer);

        var decoded = Assert.IsType<ChunkDataPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(0, decoded.ChunkX);
        Assert.Equal(0, decoded.ChunkZ);
        Assert.Single(decoded.Heightmaps);
        Assert.Equal(4, decoded.Heightmaps[0].Type);
        Assert.NotEmpty(decoded.Data);
        Assert.Empty(decoded.BlockEntities);
        Assert.Empty(decoded.Light.SkyLightMask);
        Assert.Empty(decoded.Light.BlockLightArrays);
    }

    [Fact]
    public void ChunkDataWithBlockEntityAndLightRoundTrips()
    {
        var nbt = new NbtCompound(
        [
            new NbtNamedTag("custom", new NbtString("value")),
        ]);
        var blockEntity = new ChunkBlockEntity(0x12, 70, 1, nbt);
        byte[] skyArray = new byte[2048];
        skyArray[0] = 0x7F;

        var packet = new ChunkDataPacket(
            chunkX: -1,
            chunkZ: 2,
            heightmaps: [],
            data: [],
            blockEntities: [blockEntity],
            light: new LightData([0x02], [0x04], [], [], [skyArray], []));

        using var writer = new PacketWriterScope();
        ChunkDataCodec.Instance.Encode(packet, writer.Writer);

        var decoded = Assert.IsType<ChunkDataPacket>(
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(-1, decoded.ChunkX);
        Assert.Equal(2, decoded.ChunkZ);
        Assert.Single(decoded.BlockEntities);
        Assert.Equal(0x12, decoded.BlockEntities[0].PackedXz);
        Assert.Equal(70, decoded.BlockEntities[0].Y);
        Assert.Equal(1, decoded.BlockEntities[0].Type);
        Assert.Equal("value", Assert.IsType<NbtString>(
            Assert.IsType<NbtCompound>(decoded.BlockEntities[0].Data).Tags[0].Value).Value);
        Assert.Equal([0x02], decoded.Light.SkyLightMask);
        Assert.Equal([0x04], decoded.Light.BlockLightMask);
        Assert.Single(decoded.Light.SkyLightArrays);
        Assert.Equal(2048, decoded.Light.SkyLightArrays[0].Length);
        Assert.Equal(0x7F, decoded.Light.SkyLightArrays[0][0]);
    }

    [Fact]
    public void ChunkDataTruncatedThrows()
    {
        byte[] payload = [46, 0x00, 0x00]; // packet ID only

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, payload));
    }

    [Fact]
    public void ChunkDataNegativeHeightmapCountThrows()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteVarInt(46);
        writer.Writer.WriteInt(0);
        writer.Writer.WriteInt(0);
        writer.Writer.WriteVarInt(-1); // negative heightmap count

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void ChunkDataTrailingBytesThrows()
    {
        var original = CreateMinimalPacket();
        using var writer = new PacketWriterScope();
        ChunkDataCodec.Instance.Encode(original, writer.Writer);
        writer.Writer.WriteByte(0x00);

        Assert.Throws<InvalidDataException>(() =>
            PacketRegistry.CreateDefault().Decode(_play, PacketDirection.Clientbound, writer.Bytes));
    }

    [Fact]
    public void CodecMetadataMatchesGeneratedData()
    {
        Assert.Equal(ConnectionState.Play, ChunkDataCodec.Instance.State);
        Assert.Equal(PacketDirection.Clientbound, ChunkDataCodec.Instance.Direction);
        Assert.Equal(46, ChunkDataCodec.Instance.PacketId);
    }

    [Theory]
    // java.util.BitSet wire format (26.3 ByteBufCodecs.BIT_SET): VarInt byte length,
    // then little-endian bits with trailing zero bytes trimmed.
    [InlineData(new long[0], new byte[] { 0x00 })]
    [InlineData(new long[] { 0x02 }, new byte[] { 0x01, 0x02 })] // bit 1
    [InlineData(new long[] { 0x04 }, new byte[] { 0x01, 0x04 })] // bit 2
    [InlineData(new long[] { 0x2000001 }, new byte[] { 0x04, 0x01, 0x00, 0x00, 0x02 })] // bits 0 and 25
    [InlineData(new long[] { 0x1FFFFFE }, new byte[] { 0x04, 0xFE, 0xFF, 0xFF, 0x01 })] // bits 1..24
    public void BitSetWireFormatMatchesJavaUtilBitSet(long[] mask, byte[] expectedBytes)
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteBitSet(mask);

        Assert.Equal(expectedBytes, writer.Bytes);

        var reader = new PacketReader(writer.Bytes);
        Assert.Equal(mask, reader.ReadBitSet());
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void BitSetTruncatedThrows()
    {
        // Declared byte length 5 but only one byte follows.
        var reader = new PacketReader(new byte[] { 0x05, 0xAA });

        bool caught = false;
        try
        {
            _ = reader.ReadBitSet();
        }
        catch (InvalidDataException)
        {
            caught = true;
        }

        Assert.True(caught);
    }

    [Fact]
    public void LongArrayWireFormatPrefixedByLongCount()
    {
        using var writer = new PacketWriterScope();
        writer.Writer.WriteLongArray([0x0102030405060708]);

        // VarInt 1 (one long) then the big-endian long.
        Assert.Equal([0x01, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08], writer.Bytes);

        var reader = new PacketReader(writer.Bytes);
        Assert.Equal([0x0102030405060708], reader.ReadLongArray());
    }
}
