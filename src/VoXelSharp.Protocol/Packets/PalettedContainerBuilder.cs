using VoXelSharp.Protocol.Chunk;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Builds <see cref="PalettedContainer"/> instances from an array of global block state IDs.
/// Used when constructing chunk sections for the wire (both the initial flat world and mutated
/// chunks after block changes). Format selection follows docs/data/chunk_format.md "Palette formats":
/// all entries equal -&gt; Single valued (BPE 0); up to 256 distinct -&gt; Indirect (BPE 4..8);
/// more -&gt; Direct (BPE 15).
/// </summary>
public static class PalettedContainerBuilder
{
    /// <summary>Builds a block-state paletted container from <paramref name="globalIds"/> (4096 entries, x then z then y).</summary>
    public static PalettedContainer BuildBlocks(int[] globalIds)
    {
        ArgumentNullException.ThrowIfNull(globalIds);
        if (globalIds.Length != ChunkDataIO.SectionEntryCount)
        {
            throw new ArgumentException(
                $"A block section has exactly {ChunkDataIO.SectionEntryCount} entries.", nameof(globalIds));
        }

        // Collect distinct states in first-seen order; this also detects the single-valued case.
        var paletteList = new List<int>();
        var paletteByState = new Dictionary<int, int>();
        for (int i = 0; i < globalIds.Length; i++)
        {
            int id = globalIds[i];
            if (!paletteByState.TryGetValue(id, out _))
            {
                paletteByState[id] = paletteList.Count;
                paletteList.Add(id);
            }
        }

        if (paletteList.Count == 1)
        {
            return new PalettedContainer(0, [paletteList[0]], ChunkDataIO.SectionEntryCount, []);
        }

        if (paletteList.Count <= 256)
        {
            int bits = Math.Max(4, BitsFor(paletteList.Count));
            int[] indices = new int[globalIds.Length];
            for (int i = 0; i < globalIds.Length; i++)
            {
                indices[i] = paletteByState[globalIds[i]];
            }

            return new PalettedContainer(bits, [.. paletteList], ChunkDataIO.SectionEntryCount, indices);
        }

        // More distinct states than an indirect palette can hold: direct/global IDs.
        return new PalettedContainer(PalettedContainer.DirectBlockBitsPerEntry, [], ChunkDataIO.SectionEntryCount, [.. globalIds]);
    }

    /// <summary>Smallest <c>b</c> such that <c>2^b &gt;= count</c>.</summary>
    private static int BitsFor(int count)
    {
        int bits = 0;
        while ((1 << bits) < count)
        {
            bits++;
        }

        return bits;
    }
}
