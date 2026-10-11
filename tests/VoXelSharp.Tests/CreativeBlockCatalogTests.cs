using VoXelSharp.World;

namespace VoXelSharp.Tests;

public class CreativeBlockCatalogTests
{
    [Fact]
    public void MapsKnownItemsToBlockStates()
    {
        Assert.Equal(BlockStateIds.Stone, CreativeBlockCatalog.BlockStateIdForItem(1));
        Assert.Equal(BlockStateIds.GrassBlock, CreativeBlockCatalog.BlockStateIdForItem(54));
        Assert.Equal(BlockStateIds.Dirt, CreativeBlockCatalog.BlockStateIdForItem(55));
        Assert.Equal(BlockStateIds.Bedrock, CreativeBlockCatalog.BlockStateIdForItem(87));
    }

    [Fact]
    public void UnknownItemReturnsMinusOne()
    {
        Assert.Equal(-1, CreativeBlockCatalog.BlockStateIdForItem(9999));
    }
}
