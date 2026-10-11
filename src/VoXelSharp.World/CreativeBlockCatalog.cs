namespace VoXelSharp.World;

/// <summary>
/// Maps creative-mode inventory item IDs to the block state they place (M5 "block placement").
/// Item IDs come from the <c>minecraft:item</c> registry (server reports: stone=1, grass_block=54,
/// dirt=55, bedrock=87); block state IDs come from <c>docs/data/blocks.json</c>. This is a starter
/// catalog covering the flat world's own blocks; "creative inventory basics" (M5) will later drive
/// this from full registry data.
/// </summary>
public static class CreativeBlockCatalog
{
    /// <summary>Returns the block state ID placed by the given item registry ID, or -1 when unknown.</summary>
    public static int BlockStateIdForItem(int itemId)
        => itemId switch
        {
            // Item registry IDs (server reports) -> block state IDs (docs/data/blocks.json).
            1 => BlockStateIds.Stone,       // item minecraft:stone -> block stone (1)
            54 => BlockStateIds.GrassBlock, // item minecraft:grass_block -> block grass_block (9)
            55 => BlockStateIds.Dirt,       // item minecraft:dirt -> block dirt (10)
            87 => BlockStateIds.Bedrock,    // item minecraft:bedrock -> block bedrock (88)
            _ => -1,
        };

    /// <summary>Returns the item registry ID a broken block drops, or -1 for blocks that drop nothing.</summary>
    public static int ItemIdForBlockState(int blockStateId)
        => blockStateId switch
        {
            // Block state IDs (docs/data/blocks.json) -> item registry IDs (server reports).
            // Bedrock drops nothing (unbreakable); grass_block drops itself here for simplicity.
            BlockStateIds.Stone => 1,
            BlockStateIds.GrassBlock => 54,
            BlockStateIds.Dirt => 55,
            _ => -1,
        };
}
