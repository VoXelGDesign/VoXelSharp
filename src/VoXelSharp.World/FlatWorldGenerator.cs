using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.World;

/// <summary>
/// The result of generating one chunk column: its sections, heightmaps and light data,
/// ready for serialization by <see cref="ChunkDataIO"/> and <see cref="ChunkDataPacket"/>.
/// </summary>
public sealed record FlatWorldChunk(
    int ChunkX,
    int ChunkZ,
    ChunkSection[] Sections,
    Heightmap[] Heightmaps,
    LightData Light);

/// <summary>
/// Generates a minimal flat overworld (Minecraft 26.3 / protocol 777). The world spans
/// min Y = -64, height 384 (24 sections of 16). Column layout, bottom to top:
/// 1 bedrock (y -64), 2 dirt (y -63..-62), 1 grass block (y -61), air above.
/// Block state IDs come from <see cref="BlockStateIds"/> (docs/data/blocks.json).
/// </summary>
public static class FlatWorldGenerator
{
    /// <summary>Minimum buildable Y for the vanilla overworld (docs/data/registries.json dimension_type).</summary>
    public const int MinY = -64;

    /// <summary>World height in blocks (vanilla overworld).</summary>
    public const int WorldHeight = 384;

    /// <summary>Number of 16-high sections: WorldHeight / 16.</summary>
    public const int SectionCount = WorldHeight / 16;

    /// <summary>
    /// Global biome ID for plains. The client assigns IDs from the <c>worldgen/biome</c>
    /// Registry Data sent during Configuration; the server advertises <c>minecraft:plains</c>
    /// as the single entry (ServerConfigurationDataProvider), so its global ID is 0.
    /// </summary>
    public const int PlainsBiomeGlobalId = 0;

    /// <summary>The surface grass block's Y coordinate (topmost occupied layer).</summary>
    public const int SurfaceY = -61;

    /// <summary>Number of block entries in one 16x16x16 section (docs/data/chunk_format.md).</summary>
    public const int SectionEntryCount = 16 * 16 * 16;

    /// <summary>Generates the flat world chunk at the given column coordinates.</summary>
    public static FlatWorldChunk Generate(int chunkX, int chunkZ)
    {
        var sections = new ChunkSection[SectionCount];

        // Bottom section (y = -64..-49) holds the terrain; all higher sections are air.
        sections[0] = BuildTerrainSection();
        for (int s = 1; s < SectionCount; s++)
        {
            sections[s] = BuildAirSection();
        }

        return new FlatWorldChunk(chunkX, chunkZ, sections, BuildHeightmaps(), BuildLightData());
    }

    private static ChunkSection BuildTerrainSection()
    {
        // Order on the wire: x fastest, then z, then y (docs/data/chunk_format.md).
        var globalIds = new int[SectionEntryCount];
        Array.Fill(globalIds, BlockStateIds.Air);

        for (int y = 0; y < 16; y++)
        {
            int blockStateId = y switch
            {
                0 => BlockStateIds.Bedrock,
                1 or 2 => BlockStateIds.Dirt,
                3 => BlockStateIds.GrassBlock,
                _ => BlockStateIds.Air,
            };

            int baseIndex = y * 16 * 16;
            Array.Fill(globalIds, blockStateId, baseIndex, 16 * 16);
        }

        // 4 distinct states -> indirect format is valid at the blocks minimum BPE of 4.
        int bitsPerEntry = 4;
        int[] palette = BuildPalette(globalIds, out int[] indices);
        var blocks = new PalettedContainer(bitsPerEntry, palette, SectionEntryCount, indices);

        var biomes = new PalettedContainer(0, [PlainsBiomeGlobalId], 64, []);

        const short nonAirCount = 4 * 16 * 16; // bedrock + 2 dirt + grass per column
        return new ChunkSection(nonAirCount, 0, blocks, biomes);
    }

    private static ChunkSection BuildAirSection()
    {
        var blocks = new PalettedContainer(0, [BlockStateIds.Air], SectionEntryCount, []);
        var biomes = new PalettedContainer(0, [PlainsBiomeGlobalId], 64, []);
        return new ChunkSection(0, 0, blocks, biomes);
    }

    private static int[] BuildPalette(int[] globalIds, out int[] indices)
    {
        var paletteList = new List<int>();
        var paletteByState = new Dictionary<int, int>();
        indices = new int[globalIds.Length];

        for (int i = 0; i < globalIds.Length; i++)
        {
            int id = globalIds[i];
            if (!paletteByState.TryGetValue(id, out int paletteIndex))
            {
                paletteIndex = paletteList.Count;
                paletteList.Add(id);
                paletteByState[id] = paletteIndex;
            }

            indices[i] = paletteIndex;
        }

        return [.. paletteList];
    }

    /// <summary>
    /// Builds WORLD_SURFACE (type 1) and MOTION_BLOCKING (type 4) heightmaps. Every column's
    /// topmost occupied block is the surface grass at world Y = <see cref="SurfaceY"/>, so the
    /// height value is SurfaceY - MinY + 1 for all 256 columns (x fastest).
    /// </summary>
    public static Heightmap[] BuildHeightmaps()
    {
        int height = SurfaceY - MinY + 1; // 4
        var columnHeights = new int[16 * 16];
        Array.Fill(columnHeights, height);

        long[] data = ChunkDataIO.EncodeHeightmap(WorldHeight, columnHeights);
        return [new Heightmap(1, data), new Heightmap(4, data)];
    }

    /// <summary>
    /// Builds light data with full sky light (level 15) in every in-world section and no block
    /// light. The sky light mask has 26 bits (24 sections + 2); bits 1..24 cover the in-world
    /// sections. Each sky light array is 2048 bytes (half a byte per block light value) filled
    /// with 0xFF, i.e. every nibble is 15.
    /// </summary>
    public static LightData BuildLightData()
    {
        long skyLightMask = 0;
        for (int bit = 1; bit <= SectionCount; bit++)
        {
            skyLightMask |= 1L << bit;
        }

        var skyArrays = new byte[SectionCount][];
        for (int i = 0; i < SectionCount; i++)
        {
            var array = new byte[2048];
            Array.Fill(array, (byte)0xFF);
            skyArrays[i] = array;
        }

        return new LightData(
            [skyLightMask],
            [],
            [],
            [],
            skyArrays,
            []);
    }
}
