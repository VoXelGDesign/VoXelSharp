using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Nbt;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A chunk section (docs/data/chunk_format.md "Chunk Section structure"): 16x16x16 blocks
/// plus biome regions, serialized with two paletted containers.
/// </summary>
public sealed record ChunkSection(short BlockCount, short FluidCount, PalettedContainer Blocks, PalettedContainer Biomes);

/// <summary>
/// A heightmap (docs/data/chunk_format.md "Heightmap structure"). Data is a Prefixed Array
/// of Long packed with <c>ceil(log2(world_height + 1))</c> bits per entry, 256 entries
/// (x fastest). Type: 1 = WORLD_SURFACE, 4 = MOTION_BLOCKING, 5 = MOTION_BLOCKING_NO_LEAVES.
/// </summary>
public sealed record Heightmap(int Type, long[] Data);

/// <summary>A block entity entry inside a chunk (packets.json "Block Entities").</summary>
public sealed record ChunkBlockEntity(byte PackedXz, short Y, int Type, NbtCompound Data);

/// <summary>
/// Light Data (docs/data/chunk_format.md "Light Data"), shared by Chunk Data and Update Light
/// and Update Light. The masks are BitSets; each light array is a Prefixed Array (2048) of Byte.
/// </summary>
public sealed record LightData(
    long[] SkyLightMask,
    long[] BlockLightMask,
    long[] EmptySkyLightMask,
    long[] EmptyBlockLightMask,
    byte[][] SkyLightArrays,
    byte[][] BlockLightArrays);

/// <summary>
/// Walks the chunk-section wire representation (the "Data" payload of Chunk Data and Update
/// Light). The section array is NOT length-prefixed; its count is derived from the world
/// height by the caller. Sections are encoded with a nested writer so the outer "Data" field
/// can be written as a VarInt-length-prefixed byte array.
/// </summary>
public static class ChunkDataIO
{
    /// <summary>Encodes the section array bottom-to-top into a byte payload.</summary>
    public static byte[] EncodeSections(ChunkSection[] sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        var writer = new PacketWriter();
        foreach (ChunkSection section in sections)
        {
            writer.WriteShort(section.BlockCount);
            writer.WriteShort(section.FluidCount);
            section.Blocks.WriteBody(writer);
            section.Biomes.WriteBody(writer);
        }

        return writer.ToArray();
    }

    /// <summary>
    /// Decodes <paramref name="sectionCount"/> sections from <paramref name="payload"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the payload is malformed or truncated.</exception>
    public static ChunkSection[] DecodeSections(byte[] payload, int sectionCount)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (sectionCount < 0)
        {
            throw new InvalidDataException($"Negative section count ({sectionCount}).");
        }

        var reader = new PacketReader(payload);
        var sections = new ChunkSection[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            short blockCount = reader.ReadShort();
            short fluidCount = reader.ReadShort();

            int blockBpe = reader.ReadByte();
            PalettedContainer blocks = PalettedContainer.ReadBody(ref reader, blockBpe, SectionEntryCount);

            int biomeBpe = reader.ReadByte();
            PalettedContainer biomes = PalettedContainer.ReadBody(ref reader, biomeBpe, BiomeEntryCount);

            sections[i] = new ChunkSection(blockCount, fluidCount, blocks, biomes);
        }

        if (reader.Remaining > 0)
        {
            throw new InvalidDataException(
                $"{reader.Remaining} trailing byte(s) after chunk sections.");
        }

        return sections;
    }

    /// <summary>Number of block entries in a section (16x16x16).</summary>
    public const int SectionEntryCount = 4096;

    /// <summary>Number of biome entries in a section (4x4x4).</summary>
    public const int BiomeEntryCount = 64;

    /// <summary>
    /// Computes the bits per entry for a heightmap:
    /// the smallest <c>b</c> such that <c>2^b &gt;= world_height + 1</c>
    /// (docs/data/chunk_format.md: <c>ceil(log2(world_height + 1))</c>).
    /// </summary>
    public static int HeightmapBitsPerEntry(int worldHeight)
    {
        if (worldHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(worldHeight), "World height cannot be negative.");
        }

        int required = worldHeight + 1;
        int bits = 0;
        while ((1L << bits) < required)
        {
            bits++;
        }

        return bits == 0 ? 1 : bits;
    }

    /// <summary>Encodes a full heightmap Data array from per-column heights (256 entries, x fastest).</summary>
    public static long[] EncodeHeightmap(int worldHeight, int[] columnHeights)
    {
        ArgumentNullException.ThrowIfNull(columnHeights);
        if (columnHeights.Length != 256)
        {
            throw new ArgumentException("A heightmap has exactly 256 entries.", nameof(columnHeights));
        }

        int bpe = HeightmapBitsPerEntry(worldHeight);
        return DataArrayPacker.Pack(columnHeights, bpe);
    }

    /// <summary>Maximum size guard for an encoded chunk: 2,097,151 bytes (frame limit applies).</summary>
    public const int MaxChunkPayload = 2_097_151;
}
