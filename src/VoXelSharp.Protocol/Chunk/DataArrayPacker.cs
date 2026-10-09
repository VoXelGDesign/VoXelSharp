namespace VoXelSharp.Protocol.Chunk;

/// <summary>
/// Packs small int entries into the compact 64-bit array used by paletted containers
/// (docs/data/chunk_format.md "Data Array format", wiki 2026-10-09). Entries are packed
/// with the first entry in the least significant bits of each long; an entry never spans
/// two longs, so the remainder of each long is padding on the MSB side. The array length is
/// not sent on the wire; it is derived from the bits-per-entry and the entry count.
/// </summary>
public static class DataArrayPacker
{
    /// <summary>
    /// Number of entries that fit in one 64-bit long for the given bits-per-entry.
    /// </summary>
    public static int EntriesPerLong(int bitsPerEntry) => 64 / bitsPerEntry;

    /// <summary>
    /// Number of longs required to store <paramref name="entryCount"/> entries at
    /// <paramref name="bitsPerEntry"/> bits each. The intermediate rounding to whole longs
    /// matters: <c>entryCount * bitsPerEntry / 64</c> would be wrong because of padding.
    /// </summary>
    public static int LongCount(int entryCount, int bitsPerEntry)
    {
        if (entryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryCount), "Entry count cannot be negative.");
        }

        int entriesPerLong = EntriesPerLong(bitsPerEntry);
        return (entryCount + entriesPerLong - 1) / entriesPerLong;
    }

    /// <summary>
    /// Packs <paramref name="entries"/> into a long array using <paramref name="bitsPerEntry"/>
    /// bits per entry (LSB-first within each long, MSB padding).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bitsPerEntry"/> is not in [1, 63], or an entry does not fit
    /// in the bit width.
    /// </exception>
    public static long[] Pack(int[] entries, int bitsPerEntry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ValidateBitsPerEntry(bitsPerEntry);

        long mask = (1L << bitsPerEntry) - 1;
        long[] data = new long[LongCount(entries.Length, bitsPerEntry)];
        int entriesPerLong = EntriesPerLong(bitsPerEntry);

        for (int i = 0; i < entries.Length; i++)
        {
            if ((long)entries[i] < 0 || (long)entries[i] > mask)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(entries),
                    $"Entry {entries[i]} at index {i} does not fit in {bitsPerEntry} bits.");
            }

            int longIndex = i / entriesPerLong;
            int bitIndex = (i % entriesPerLong) * bitsPerEntry;
            data[longIndex] |= (long)entries[i] << bitIndex;
        }

        return data;
    }

    /// <summary>
    /// Unpacks <paramref name="entryCount"/> entries from the compact long array.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="data"/> is too small for the requested count and bit width.
    /// </exception>
    public static int[] Unpack(long[] data, int entryCount, int bitsPerEntry)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateBitsPerEntry(bitsPerEntry);
        if (entryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryCount), "Entry count cannot be negative.");
        }

        if (data.Length < LongCount(entryCount, bitsPerEntry))
        {
            throw new ArgumentException(
                $"Data array of {data.Length} longs is too small for {entryCount} entries at {bitsPerEntry} bits each.");
        }

        long mask = (1L << bitsPerEntry) - 1;
        int entriesPerLong = EntriesPerLong(bitsPerEntry);
        var entries = new int[entryCount];

        for (int i = 0; i < entryCount; i++)
        {
            int longIndex = i / entriesPerLong;
            int bitIndex = (i % entriesPerLong) * bitsPerEntry;
            entries[i] = (int)((data[longIndex] >> bitIndex) & mask);
        }

        return entries;
    }

    private static void ValidateBitsPerEntry(int bitsPerEntry)
    {
        // Entries are non-negative ints; the protocol's Direct format uses at most 31 bits
        // (wiki: "This increase can go up to 31 bits per entry" for modded registries).
        if (bitsPerEntry < 1 || bitsPerEntry > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), "Bits per entry must be in [1, 31].");
        }
    }
}
