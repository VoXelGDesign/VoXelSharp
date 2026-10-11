using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.World;

/// <summary>
/// A mutable flat world (Minecraft 26.3 / protocol 777). Each chunk column begins as the
/// generator's flat terrain (bedrock/dirt/grass over air) and can be mutated with
/// <see cref="SetBlock"/>. Chunk packets are re-serialized from the current block states, so
/// the client sees changes when a chunk is (re)sent, while <c>block_update</c> pushes immediate
/// single-block changes.
///
/// The column store keeps the global block state ID per block in a flat <see cref="int"/> array
/// sized <see cref="FlatWorldGenerator.SectionCount"/> × <see cref="ChunkDataIO.SectionEntryCount"/>
/// (x fastest, then z, then y within each section). Sections are rebuilt with
/// <see cref="PalettedContainerBuilder"/> after a mutation; heightmaps, light and biomes always
/// remain the flat-world defaults (the terrain surfaces are unchanged by M5 mutations from the
/// air layer, so the surface heightmaps stay valid).
/// </summary>
public sealed class FlatWorld : IWorldAccess
{
    /// <summary>Contains the mutable blocks of one chunk column.</summary>
    private sealed class ChunkColumn
    {
        private readonly int[] _blocks;

        public ChunkColumn()
        {
            _blocks = new int[FlatWorldGenerator.SectionCount * ChunkDataIO.SectionEntryCount];
        }

        public void InitializeFrom(FlatWorldChunk template)
        {
            for (int sectionIndex = 0; sectionIndex < template.Sections.Length; sectionIndex++)
            {
                int sectionOffset = sectionIndex * ChunkDataIO.SectionEntryCount;
                for (int i = 0; i < ChunkDataIO.SectionEntryCount; i++)
                {
                    _blocks[sectionOffset + i] = template.Sections[sectionIndex].Blocks.GlobalIdAt(i);
                }
            }
        }

        /// <summary>Returns the global block state ID at a local entry index (0..SectionCount*4096).</summary>
        public int this[int index] => _blocks[index];

        public void Set(int index, int globalStateId) => _blocks[index] = globalStateId;

        public ChunkSection[] BuildSections()
        {
            var sections = new ChunkSection[FlatWorldGenerator.SectionCount];
            for (int sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                int sectionOffset = sectionIndex * ChunkDataIO.SectionEntryCount;
                var sectionBlocks = new int[ChunkDataIO.SectionEntryCount];
                Array.Copy(_blocks, sectionOffset, sectionBlocks, 0, ChunkDataIO.SectionEntryCount);

                short blockCount = 0;
                foreach (int id in sectionBlocks)
                {
                    if (id != BlockStateIds.Air)
                    {
                        blockCount++;
                    }
                }

                var blocks = PalettedContainerBuilder.BuildBlocks(sectionBlocks);
                var biomes = new PalettedContainer(0, [FlatWorldGenerator.PlainsBiomeGlobalId], ChunkDataIO.BiomeEntryCount, []);
                sections[sectionIndex] = new ChunkSection(blockCount, 0, blocks, biomes);
            }

            return sections;
        }
    }

    private readonly ConcurrentDictionary<(int ChunkX, int ChunkZ), ChunkColumn> _columns = new();

    /// <summary>Returns the global block state ID at the given world position.</summary>
    public int GetBlock(int x, int y, int z)
    {
        if (!IsInWorld(y))
        {
            return BlockStateIds.Air;
        }

        int chunkX = Math.DivRem(x, 16, out int localX);
        int chunkZ = Math.DivRem(z, 16, out int localZ);
        NormalizeLocal(chunkX, ref localX, out chunkX);
        NormalizeLocal(chunkZ, ref localZ, out chunkZ);

        ChunkColumn column = _columns.GetOrAdd((chunkX, chunkZ), key => CreateSeededColumn(key.ChunkX, key.ChunkZ));

        int localY = y - FlatWorldGenerator.MinY;
        return column[BuildIndex(localX, localY, localZ)];
    }

    /// <summary>Sets the block state ID at the given world position; positions outside the world are ignored.</summary>
    public void SetBlock(int x, int y, int z, int globalStateId)
    {
        if (!IsInWorld(y))
        {
            return;
        }

        int chunkX = Math.DivRem(x, 16, out int localX);
        int chunkZ = Math.DivRem(z, 16, out int localZ);
        NormalizeLocal(chunkX, ref localX, out chunkX);
        NormalizeLocal(chunkZ, ref localZ, out chunkZ);

        ChunkColumn column = _columns.GetOrAdd((chunkX, chunkZ), key => CreateSeededColumn(key.ChunkX, key.ChunkZ));
        int localY = y - FlatWorldGenerator.MinY;
        column.Set(BuildIndex(localX, localY, localZ), globalStateId);
    }

    /// <summary>Builds the chunk column for the given chunk coordinates (lazily seeded from the flat terrain).</summary>
    public FlatWorldChunk GetChunk(int chunkX, int chunkZ)
    {
        ChunkColumn column = _columns.GetOrAdd((chunkX, chunkZ), key => CreateSeededColumn(key.ChunkX, key.ChunkZ));

        return new FlatWorldChunk(
            chunkX,
            chunkZ,
            column.BuildSections(),
            FlatWorldGenerator.BuildHeightmaps(),
            FlatWorldGenerator.BuildLightData());
    }

    private static ChunkColumn CreateSeededColumn(int chunkX, int chunkZ)
    {
        var column = new ChunkColumn();
        column.InitializeFrom(FlatWorldGenerator.Generate(chunkX, chunkZ));
        return column;
    }

    private static bool IsInWorld(int y) => y >= FlatWorldGenerator.MinY && y < FlatWorldGenerator.MinY + FlatWorldGenerator.WorldHeight;

    private static void NormalizeLocal(int chunk, ref int local, out int normalizedChunk)
    {
        if (local < 0)
        {
            chunk--;
            local += 16;
        }

        normalizedChunk = chunk;
    }

    private static int BuildIndex(int localX, int localY, int localZ)
    {
        // Per section: (localY * 16 + localZ) * 16 + localX, x fastest.
        int section = Math.DivRem(localY, 16, out int yInSection);
        return (section * ChunkDataIO.SectionEntryCount) + ((yInSection * 16) + localZ) * 16 + localX;
    }
}
