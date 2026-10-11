using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.World;

namespace VoXelSharp.Tests;

public class FlatWorldTests
{
    [Fact]
    public void GetBlockReturnsGeneratedTerrainByDefault()
    {
        var world = new FlatWorld();

        Assert.Equal(BlockStateIds.Bedrock, world.GetBlock(0, -64, 0));
        Assert.Equal(BlockStateIds.Dirt, world.GetBlock(0, -63, 0));
        Assert.Equal(BlockStateIds.GrassBlock, world.GetBlock(0, -61, 0));
        Assert.Equal(BlockStateIds.Air, world.GetBlock(0, -60, 0));
        Assert.Equal(BlockStateIds.Air, world.GetBlock(0, 100, 0));
    }

    [Fact]
    public void GetBlockBelowAndAboveWorldBoundsReturnsAir()
    {
        var world = new FlatWorld();

        Assert.Equal(BlockStateIds.Air, world.GetBlock(0, FlatWorldGenerator.MinY - 1, 0));
        Assert.Equal(BlockStateIds.Air, world.GetBlock(0, FlatWorldGenerator.MinY + FlatWorldGenerator.WorldHeight, 0));
    }

    [Fact]
    public void SetBlockOverwritesAndGetBlockReadsBack()
    {
        var world = new FlatWorld();

        world.SetBlock(0, -60, 0, BlockStateIds.Stone);
        Assert.Equal(BlockStateIds.Stone, world.GetBlock(0, -60, 0));
    }

    [Fact]
    public void NegativeCoordinatesMapToTheCorrectChunk()
    {
        var world = new FlatWorld();

        // x=-1, z=-1 is in chunk (-1,-1) at local (15,15); y=-61 is the grass surface.
        world.SetBlock(-1, -61, -1, BlockStateIds.Stone);
        Assert.Equal(BlockStateIds.Stone, world.GetBlock(-1, -61, -1));
    }

    [Fact]
    public void SetBlockOutsideWorldBoundsIsIgnored()
    {
        var world = new FlatWorld();

        world.SetBlock(0, FlatWorldGenerator.MinY - 1, 0, BlockStateIds.Stone);
        world.SetBlock(0, FlatWorldGenerator.MinY + FlatWorldGenerator.WorldHeight, 0, BlockStateIds.Stone);

        Assert.Equal(BlockStateIds.Air, world.GetBlock(0, FlatWorldGenerator.MinY - 1, 0));
        Assert.Equal(BlockStateIds.Air, world.GetBlock(0, FlatWorldGenerator.MinY + FlatWorldGenerator.WorldHeight, 0));
    }

    [Fact]
    public void GetChunkReflectsMutationsAndMatchesGeneratorInitially()
    {
        var world = new FlatWorld();
        FlatWorldChunk initial = world.GetChunk(0, 0);

        // Initial terrain matches the generator exactly (same sections, heightmaps, light).
        FlatWorldChunk generated = FlatWorldGenerator.Generate(0, 0);
        Assert.Equal(
            ChunkDataIO.EncodeSections(generated.Sections),
            ChunkDataIO.EncodeSections(initial.Sections));
        Assert.Equal(4 * 16 * 16, initial.Sections[0].BlockCount);

        // Mutate a block above the surface, then re-send the chunk: the section now carries it.
        world.SetBlock(0, -60, 0, BlockStateIds.Stone);
        FlatWorldChunk mutated = world.GetChunk(0, 0);
        Assert.NotEqual(ChunkDataIO.EncodeSections(initial.Sections), ChunkDataIO.EncodeSections(mutated.Sections));
        Assert.Contains(BlockStateIds.Stone, mutated.Sections[0].Blocks.Palette);
    }

    [Fact]
    public void GetChunkIsStableWithoutMutations()
    {
        var world = new FlatWorld();
        FlatWorldChunk first = world.GetChunk(4, -2);
        FlatWorldChunk second = world.GetChunk(4, -2);

        Assert.Equal(
            ChunkDataIO.EncodeSections(first.Sections),
            ChunkDataIO.EncodeSections(second.Sections));
    }

    private static int SectionByteCount(FlatWorldChunk chunk)
        => ChunkDataIO.EncodeSections(chunk.Sections).Length;
}
