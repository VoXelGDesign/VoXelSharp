using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.World;

namespace VoXelSharp.Tests;

public class FlatWorldGeneratorTests
{
    [Fact]
    public void GeneratesTheExpectedSectionCount()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(0, 0);

        Assert.Equal(FlatWorldGenerator.SectionCount, chunk.Sections.Length);
        Assert.Equal(24, chunk.Sections.Length); // 384 / 16
    }

    [Fact]
    public void TerrainSectionLayoutMatchesClassicFlat()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(3, -7);

        ChunkSection terrain = chunk.Sections[0];

        // One bedrock + two dirt + one grass per column: 4 non-air layers x 256 columns.
        Assert.Equal(4 * 16 * 16, terrain.BlockCount);
        Assert.Equal(0, terrain.FluidCount);

        // Blocks palette: bedrock, dirt, grass, then air (first-seen order, y-major).
        Assert.Equal(4, terrain.Blocks.BitsPerEntry);
        Assert.Equal(
            [BlockStateIds.Bedrock, BlockStateIds.Dirt, BlockStateIds.GrassBlock, BlockStateIds.Air],
            terrain.Blocks.Palette);

        // Biomes are single-valued plains (global ID 0).
        Assert.Equal(0, terrain.Biomes.BitsPerEntry);
        Assert.Equal([FlatWorldGenerator.PlainsBiomeGlobalId], terrain.Biomes.Palette);
    }

    [Fact]
    public void TerrainSectionBlockPlacementWithinSection()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(0, 0);
        ChunkSection terrain = chunk.Sections[0];

        // Entry index: (y * 16 + z) * 16 + x  (x fastest, then z, then y).
        Assert.Equal(BlockStateIds.Bedrock, BlockIdAt(terrain, x: 0, z: 0, y: 0));
        Assert.Equal(BlockStateIds.Dirt, BlockIdAt(terrain, x: 15, z: 15, y: 1));
        Assert.Equal(BlockStateIds.Dirt, BlockIdAt(terrain, x: 0, z: 3, y: 2));
        Assert.Equal(BlockStateIds.GrassBlock, BlockIdAt(terrain, x: 7, z: 7, y: 3));
        Assert.Equal(BlockStateIds.Air, BlockIdAt(terrain, x: 0, z: 0, y: 4));
        Assert.Equal(BlockStateIds.Air, BlockIdAt(terrain, x: 15, z: 15, y: 15));
    }

    [Fact]
    public void UpperSectionsAreAllAir()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(1, 1);

        for (int s = 1; s < FlatWorldGenerator.SectionCount; s++)
        {
            ChunkSection section = chunk.Sections[s];
            Assert.Equal(0, section.BlockCount);
            Assert.Equal(0, section.FluidCount);
            Assert.Equal(0, section.Blocks.BitsPerEntry);
            Assert.Equal([BlockStateIds.Air], section.Blocks.Palette);
            Assert.Equal(0, section.Biomes.BitsPerEntry);
            Assert.Equal([FlatWorldGenerator.PlainsBiomeGlobalId], section.Biomes.Palette);
        }
    }

    [Fact]
    public void HeightmapsReportSurfaceForEveryColumn()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(-2, 5);

        Assert.Equal(2, chunk.Heightmaps.Length);
        Assert.Equal(1, chunk.Heightmaps[0].Type); // WORLD_SURFACE
        Assert.Equal(4, chunk.Heightmaps[1].Type); // MOTION_BLOCKING

        int bitsPerEntry = ChunkDataIO.HeightmapBitsPerEntry(FlatWorldGenerator.WorldHeight);
        Assert.Equal(9, bitsPerEntry);

        foreach (Heightmap heightmap in chunk.Heightmaps)
        {
            int[] heights = DataArrayPacker.Unpack(heightmap.Data, 256, bitsPerEntry);
            int expected = FlatWorldGenerator.SurfaceY - FlatWorldGenerator.MinY + 1;
            Assert.All(heights, height => Assert.Equal(expected, height));
        }
    }

    [Fact]
    public void LightDataFillsSkyLightInEverySection()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(0, 0);
        LightData light = chunk.Light;

        // Sky light mask covers the 24 in-world sections (bits 1..24).
        long mask = light.SkyLightMask[0];
        for (int bit = 1; bit <= FlatWorldGenerator.SectionCount; bit++)
        {
            Assert.True((mask & (1L << bit)) != 0, $"Sky bit {bit} should be set.");
        }

        Assert.Empty(light.BlockLightMask);
        Assert.Empty(light.EmptySkyLightMask);
        Assert.Empty(light.EmptyBlockLightMask);

        Assert.Equal(FlatWorldGenerator.SectionCount, light.SkyLightArrays.Length);
        Assert.Empty(light.BlockLightArrays);

        foreach (byte[] array in light.SkyLightArrays)
        {
            Assert.Equal(2048, array.Length); // half a byte per light value, 4096 values per section
            Assert.All(array, value => Assert.Equal(0xFF, value));
        }
    }

    [Fact]
    public void ChunkDataSectionsRoundTripThroughWireFormat()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(4, 4);

        byte[] payload = ChunkDataIO.EncodeSections(chunk.Sections);
        ChunkSection[] decoded = ChunkDataIO.DecodeSections(payload, FlatWorldGenerator.SectionCount);

        Assert.Equal(FlatWorldGenerator.SectionCount, decoded.Length);

        // Terrain section survived the round trip.
        ChunkSection terrain = decoded[0];
        Assert.Equal(4 * 16 * 16, terrain.BlockCount);
        Assert.Equal(
            [BlockStateIds.Bedrock, BlockStateIds.Dirt, BlockStateIds.GrassBlock, BlockStateIds.Air],
            terrain.Blocks.Palette);
        Assert.Equal(BlockStateIds.Bedrock, BlockIdAt(terrain, 0, 0, 0));
        Assert.Equal(BlockStateIds.GrassBlock, BlockIdAt(terrain, 5, 5, 3));
        Assert.Equal(BlockStateIds.Air, BlockIdAt(terrain, 1, 2, 5));

        // An upper air section stayed single-valued air.
        ChunkSection air = decoded[FlatWorldGenerator.SectionCount - 1];
        Assert.Equal(0, air.BlockCount);
        Assert.Equal([BlockStateIds.Air], air.Blocks.Palette);
    }

    [Fact]
    public void ChunkDataPacketRoundTripsWithGeneratedChunk()
    {
        FlatWorldChunk chunk = FlatWorldGenerator.Generate(8, -8);
        byte[] data = ChunkDataIO.EncodeSections(chunk.Sections);

        var packet = new ChunkDataPacket(
            chunk.ChunkX,
            chunk.ChunkZ,
            chunk.Heightmaps,
            data,
            [],
            chunk.Light);
        using var writer = new PacketWriterScope();
        ChunkDataCodec.Instance.Encode(packet, writer.Writer);

        var decoded = Assert.IsType<ChunkDataPacket>(
            PacketRegistry.CreateDefault().Decode(ConnectionState.Play, PacketDirection.Clientbound, writer.Bytes));

        Assert.Equal(8, decoded.ChunkX);
        Assert.Equal(-8, decoded.ChunkZ);
        Assert.Equal(2, decoded.Heightmaps.Length);
        Assert.Equal(chunk.ChunkX, decoded.ChunkX);

        ChunkSection[] sections = ChunkDataIO.DecodeSections(
            decoded.Data, FlatWorldGenerator.SectionCount);
        Assert.Equal(4 * 16 * 16, sections[0].BlockCount);
        Assert.Equal(BlockStateIds.GrassBlock, BlockIdAt(sections[0], 0, 0, 3));
    }

    [Fact]
    public void GeneratorIsDeterministicForChunkCoordinates()
    {
        FlatWorldChunk first = FlatWorldGenerator.Generate(0, 0);
        FlatWorldChunk second = FlatWorldGenerator.Generate(0, 0);

        Assert.Equal(
            ChunkDataIO.EncodeSections(first.Sections),
            ChunkDataIO.EncodeSections(second.Sections));
        Assert.Equal(first.Heightmaps[0].Data, second.Heightmaps[0].Data);
    }

    private static int BlockIdAt(ChunkSection section, int x, int z, int y)
    {
        int index = (y * 16 + z) * 16 + x;
        return section.Blocks.Palette[section.Blocks.Entries[index]];
    }
}
