using VoXelSharp.Protocol.Chunk;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A paletted container (docs/data/chunk_format.md "Paletted Container structure"), used for
/// both block states (4096 entries) and biomes (64 entries) in a chunk section.
/// Three wire formats are selected by the Bits Per Entry byte:
///  - 0: Single valued. Data array is empty; all entries equal the single global ID.
///  - small (blocks 4-8, biomes 1-3): Indirect. A local palette of global IDs is sent, and the
///    data array holds indices into that palette.
///  - large (blocks 15, biomes 7): Direct. The data array holds global IDs directly.
/// The data array length is never sent; it is derived from bits-per-entry and the entry count.
/// </summary>
public sealed class PalettedContainer
{
    /// <summary>Bits per entry for the Direct format (blocks). </summary>
    public const int DirectBlockBitsPerEntry = 15;

    /// <summary>Bits per entry for the Direct format (biomes).</summary>
    public const int DirectBiomeBitsPerEntry = 7;

    private readonly int[] _data;

    public PalettedContainer(int bitsPerEntry, int[] palette, int length, int[] data)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(data);

        if (bitsPerEntry < 0 || bitsPerEntry > 8 && bitsPerEntry != DirectBlockBitsPerEntry && bitsPerEntry != DirectBiomeBitsPerEntry)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bitsPerEntry),
                $"Bits per entry {bitsPerEntry} is not a valid paletted-container format value.");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Entry count cannot be negative.");
        }

        BitsPerEntry = bitsPerEntry;
        Palette = palette;
        Length = length;
        _data = data;
    }

    /// <summary>Bits Per Entry (unsigned byte on the wire).</summary>
    public int BitsPerEntry { get; }

    /// <summary>Local palette of global IDs (empty for the Direct format; single element when BPE is 0).</summary>
    public int[] Palette { get; }

    /// <summary>Number of entries this container stores (4096 blocks or 64 biomes).</summary>
    public int Length { get; }

    /// <summary>Unpacked entries. For Indirect PEs, these are indices into <see cref="Palette"/>; for Direct, global IDs.</summary>
    public int[] Entries
    {
        get
        {
            if (BitsPerEntry == 0)
            {
                var all = new int[Length];
                Array.Fill(all, Palette[0]);
                return all;
            }

            return _data;
        }
    }

    /// <summary>Writes the container body (Bits Per Entry, palette, packed data array).</summary>
    public void WriteBody(PacketWriter writer)
    {
        writer.WriteByte((byte)BitsPerEntry);

        if (BitsPerEntry == 0)
        {
            // Single valued: one VarInt global ID, no data array.
            writer.WriteVarInt(Palette[0]);
            return;
        }

        int entriesPerLong = DataArrayPacker.EntriesPerLong(BitsPerEntry);
        long[] packed;
        if (BitsPerEntry == DirectBlockBitsPerEntry || BitsPerEntry == DirectBiomeBitsPerEntry)
        {
            // Direct: data holds global IDs; no palette field.
            packed = DataArrayPacker.Pack(_data, BitsPerEntry);
        }
        else
        {
            // Indirect: local palette then packed indices.
            writer.WriteVarInt(Palette.Length);
            foreach (int globalId in Palette)
            {
                writer.WriteVarInt(globalId);
            }

            packed = DataArrayPacker.Pack(_data, BitsPerEntry);
        }

        foreach (long value in packed)
        {
            writer.WriteLong(value);
        }
    }

    /// <summary>
    /// Reads a paletted container body positioned after Bits Per Entry has been consumed.
    /// </summary>
    /// <param name="reader">Positioned at the palette field (Bits Per Entry already read).</param>
    /// <param name="bitsPerEntry">The container's Bits Per Entry.</param>
    /// <param name="entryCount">Number of entries (4096 for blocks, 64 for biomes).</param>
    /// <exception cref="InvalidDataException">Thrown when the payload is malformed or truncated.</exception>
    public static PalettedContainer ReadBody(ref PacketReader reader, int bitsPerEntry, int entryCount)
    {
        if (bitsPerEntry < 0)
        {
            throw new InvalidDataException($"Negative bits per entry ({bitsPerEntry}).");
        }

        if (bitsPerEntry == 0)
        {
            // Single valued: one global ID, empty data array.
            int globalId = reader.ReadVarInt();
            return new PalettedContainer(0, [globalId], entryCount, []);
        }

        int[] palette = [];
        int[] data;
        if (bitsPerEntry == DirectBlockBitsPerEntry || bitsPerEntry == DirectBiomeBitsPerEntry)
        {
            // Direct: no palette; the data array holds global IDs directly.
            data = ReadPacked(ref reader, bitsPerEntry, entryCount);
        }
        else
        {
            // Indirect: read the local palette, then packed indices.
            int paletteCount = reader.ReadVarInt();
            if (paletteCount < 0)
            {
                throw new InvalidDataException($"Negative palette size ({paletteCount}).");
            }

            palette = new int[paletteCount];
            for (int i = 0; i < paletteCount; i++)
            {
                palette[i] = reader.ReadVarInt();
            }

            data = ReadPacked(ref reader, bitsPerEntry, entryCount);
        }

        return new PalettedContainer(bitsPerEntry, palette, entryCount, data);
    }

    private static int[] ReadPacked(ref PacketReader reader, int bitsPerEntry, int entryCount)
    {
        int longCount = DataArrayPacker.LongCount(entryCount, bitsPerEntry);
        var longs = new long[longCount];
        for (int i = 0; i < longCount; i++)
        {
            longs[i] = reader.ReadLong();
        }

        return DataArrayPacker.Unpack(longs, entryCount, bitsPerEntry);
    }
}
