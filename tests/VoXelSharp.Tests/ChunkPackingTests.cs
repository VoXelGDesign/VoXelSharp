using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Tests;

public class DataArrayPackerTests
{
    [Theory]
    [InlineData(1, 64)]
    [InlineData(4, 16)]
    [InlineData(5, 12)]
    [InlineData(8, 8)]
    [InlineData(15, 4)]
    [InlineData(16, 4)]
    [InlineData(32, 2)]
    public void EntriesPerLongMatchesFloorDivision(int bits, int expected)
        => Assert.Equal(expected, DataArrayPacker.EntriesPerLong(bits));

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(12, 5, 1)]
    [InlineData(13, 5, 2)]
    [InlineData(4096, 15, 1024)]
    [InlineData(64, 7, 8)]
    [InlineData(4096, 4, 256)]
    public void LongCountUsesCeilingDivision(int entries, int bits, int expected)
        => Assert.Equal(expected, DataArrayPacker.LongCount(entries, bits));

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(31)]
    public void PackUnpackRoundTrips(int bits)
    {
        var entries = new int[200];
        var random = new Random(1234);
        long mask = (1L << bits) - 1;
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = random.Next(0, (int)mask);
        }

        long[] packed = DataArrayPacker.Pack(entries, bits);
        int[] unpacked = DataArrayPacker.Unpack(packed, entries.Length, bits);

        Assert.Equal(entries, unpacked);
    }

    [Fact]
    public void PackingMatchesWikiExample()
    {
        // docs/data/chunk_format.md "Visual example" (5 bits per block, 24 entries).
        // The wiki shows the two longs as 0x0020863148418841 and 0x01018A7260F68C87.
        int[] entries = [1, 2, 2, 3, 4, 4, 5, 6, 6, 4, 8, 0, 7, 4, 3, 13, 15, 16, 9, 14, 10, 12, 0, 2];

        long[] packed = DataArrayPacker.Pack(entries, 5);

        Assert.Equal(2, packed.Length);
        Assert.Equal(0x0020863148418841L, packed[0]);
        Assert.Equal(0x01018A7260F68C87L, packed[1]);
    }

    [Fact]
    public void UnpackMatchesWikiExample()
    {
        long[] packed = [0x0020863148418841L, 0x01018A7260F68C87L];

        int[] entries = DataArrayPacker.Unpack(packed, 24, 5);

        Assert.Equal([1, 2, 2, 3, 4, 4, 5, 6, 6, 4, 8, 0, 7, 4, 3, 13, 15, 16, 9, 14, 10, 12, 0, 2], entries);
    }

    [Fact]
    public void PackRejectsOversizedEntry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DataArrayPacker.Pack([16], 4));
    }

    [Fact]
    public void PackRejectsNegativeEntry()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DataArrayPacker.Pack([-1], 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public void PackRejectsInvalidBitsPerEntry(int bits)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DataArrayPacker.Pack([0], bits));
    }

    [Fact]
    public void UnpackRejectsShortData()
    {
        // 5 bits per entry, 100 entries needs ceil(100/12)=9 longs; provide 8.
        Assert.Throws<ArgumentException>(() => DataArrayPacker.Unpack(new long[8], 100, 5));
    }
}

public class PalettedContainerTests
{
    [Fact]
    public void SingleValuedRoundTrips()
    {
        // BPE 0: one VarInt global ID, empty data array.
        var container = new PalettedContainer(0, [7], 4096, []);

        var writer = new PacketWriter();
        container.WriteBody(writer);

        var reader = new PacketReader(writer.ToArray());
        int bpe = reader.ReadByte();
        var decoded = PalettedContainer.ReadBody(ref reader, bpe, 4096);

        Assert.Equal(0, decoded.BitsPerEntry);
        Assert.Equal(7, decoded.Palette[0]);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void IndirectRoundTrips()
    {
        // BPE 4: palette [40, 57, 0] (wiki example: stone, gravel, air), 4096 entries
        // alternating indices into the palette.
        int[] indices = new int[4096];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i % 3;
        }

        var container = new PalettedContainer(4, [40, 57, 0], 4096, indices);

        var writer = new PacketWriter();
        container.WriteBody(writer);

        var reader = new PacketReader(writer.ToArray());
        int bpe = reader.ReadByte();
        var decoded = PalettedContainer.ReadBody(ref reader, bpe, 4096);

        Assert.Equal(4, decoded.BitsPerEntry);
        Assert.Equal([40, 57, 0], decoded.Palette);
        Assert.Equal(0, reader.Remaining);
        Assert.Equal(1, decoded.Entries[1]); // index into palette
    }

    [Fact]
    public void DirectRoundTrips()
    {
        // BPE 15: 4096 global block-state IDs.
        int[] globals = new int[4096];
        for (int i = 0; i < globals.Length; i++)
        {
            globals[i] = i; // must fit in 15 bits
        }

        var container = new PalettedContainer(PalettedContainer.DirectBlockBitsPerEntry, [], 4096, globals);

        var writer = new PacketWriter();
        container.WriteBody(writer);

        var reader = new PacketReader(writer.ToArray());
        int bpe = reader.ReadByte();
        var decoded = PalettedContainer.ReadBody(ref reader, bpe, 4096);

        Assert.Equal(PalettedContainer.DirectBlockBitsPerEntry, decoded.BitsPerEntry);
        Assert.Empty(decoded.Palette);
        Assert.Equal(0, reader.Remaining);
        Assert.Equal(4095, decoded.Entries[4095]);
    }

    [Fact]
    public void SingleValuedProducesUniformEntries()
    {
        var container = new PalettedContainer(0, [3], 64, []);

        int[] entries = container.Entries;

        Assert.Equal(64, entries.Length);
        Assert.All(entries, entry => Assert.Equal(3, entry));
    }

    [Fact]
    public void IndirectEntriesArePaletteIndices()
    {
        int[] indices = [2, 0, 1, 2];
        var container = new PalettedContainer(4, [40, 57, 0], 4, indices);

        Assert.Equal([2, 0, 1, 2], container.Entries);
    }

    [Fact]
    public void NegativePaletteSizeThrows()
    {
        // BPE 4 (indirect), then -1 palette count.
        var writer = new PacketWriter();
        writer.WriteByte(4);
        writer.WriteVarInt(-1);

        bool caught = false;
        try
        {
            var reader = new PacketReader(writer.ToArray());
            int bpe = reader.ReadByte();
            PalettedContainer.ReadBody(ref reader, bpe, 4);
        }
        catch (InvalidDataException)
        {
            caught = true;
        }

        Assert.True(caught);
    }

    [Fact]
    public void InvalidBitsPerEntryRejectedByConstructor()
    {
        // 9 is not a valid format value per docs/data/chunk_format.md.
        Assert.Throws<ArgumentOutOfRangeException>(() => new PalettedContainer(9, [0], 4096, []));
    }
}
