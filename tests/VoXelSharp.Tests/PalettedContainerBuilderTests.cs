using VoXelSharp.Protocol.Packets;
using VoXelSharp.World;

namespace VoXelSharp.Tests;

public class PalettedContainerBuilderTests
{
    [Fact]
    public void SingleValuedContainerWhenAllEntriesEqual()
    {
        var container = PalettedContainerBuilder.BuildBlocks(
            Enumerable.Repeat(BlockStateIds.Air, 4096).ToArray());

        Assert.Equal(0, container.BitsPerEntry);
        Assert.Equal([BlockStateIds.Air], container.Palette);
        Assert.All(Enumerable.Range(0, 4096), i => Assert.Equal(BlockStateIds.Air, container.GlobalIdAt(i)));
    }

    [Fact]
    public void IndirectContainerForFewDistinctStates()
    {
        var ids = new int[4096];
        ids[0] = BlockStateIds.Stone;
        ids[100] = BlockStateIds.Dirt;
        var container = PalettedContainerBuilder.BuildBlocks(ids);

        Assert.Equal(4, container.BitsPerEntry);
        Assert.Equal(BlockStateIds.Stone, container.GlobalIdAt(0));
        Assert.Equal(BlockStateIds.Dirt, container.GlobalIdAt(100));
        Assert.Equal(BlockStateIds.Air, container.GlobalIdAt(2000));
    }

    [Fact]
    public void RejectsWrongEntryCount()
    {
        Assert.Throws<ArgumentException>(() => PalettedContainerBuilder.BuildBlocks(new int[10]));
    }
}
