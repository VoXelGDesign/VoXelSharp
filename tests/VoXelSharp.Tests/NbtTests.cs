using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Nbt;

namespace VoXelSharp.Tests;

public class NbtTests
{
    // Known-good Network NBT: root unnamed Compound containing a String tag "name" = "Bananrama".
    //  0x0A                          -> root Compound (Network NBT, no name length prefix)
    //    0x08  0x00 0x04  name       -> String tag named "name" (mod-UTF8, unsigned-short len 4)
    //    0x00 0x09  Bananrama        -> value, unsigned-short byte length 9
    //    0x00                         -> TAG_End
    private static readonly byte[] _knownGood =
    {
        0x0A,
        0x08, 0x00, 0x04, (byte)'n', (byte)'a', (byte)'m', (byte)'e',
        0x00, 0x09,
        (byte)'B', (byte)'a', (byte)'n', (byte)'a', (byte)'n', (byte)'r', (byte)'a', (byte)'m', (byte)'a',
        0x00,
    };

    [Fact]
    public void ReadKnownGoodNetworkNbt()
    {
        var reader = new PacketReader(_knownGood);
        var root = NbtReader.ReadRoot(ref reader);

        Assert.Equal(NbtTagType.CompoundTag, root.TagType);
        Assert.Equal(0, reader.Remaining);
        Assert.Single(root.Tags);
        var tag = Assert.IsType<NbtString>(root.Tags[0].Value);
        Assert.Equal("name", root.Tags[0].Name);
        Assert.Equal("Bananrama", tag.Value);
    }

    [Fact]
    public void WriteMatchesKnownGoodNetworkNbt()
    {
        var root = new NbtCompound([new NbtNamedTag("name", new NbtString("Bananrama"))]);

        var writer = new PacketWriter();
        NbtWriter.WriteRoot(writer, root);

        Assert.Equal(_knownGood, writer.ToArray());
    }

    [Fact]
    public void RootRoundTrips()
    {
        var root = new NbtCompound(
        [
            new NbtNamedTag("level", new NbtInt(5)),
            new NbtNamedTag("health", new NbtFloat(20.5f)),
            new NbtNamedTag("pos", new NbtList(
                NbtTagType.DoubleTag,
                [new NbtDouble(1.5), new NbtDouble(-2.25), new NbtDouble(3.0)])),
            new NbtNamedTag("ids", new NbtIntArray([1, 2, 3])),
        ]);

        var writer = new PacketWriter();
        NbtWriter.WriteRoot(writer, root);

        var reader = new PacketReader(writer.ToArray());
        var decoded = NbtReader.ReadRoot(ref reader);
        Assert.Equal(0, reader.Remaining);

        Assert.Equal(4, decoded.Tags.Count);
        Assert.Equal(5, Assert.IsType<NbtInt>(decoded.Tags[0].Value).Value);
        Assert.Equal(20.5f, Assert.IsType<NbtFloat>(decoded.Tags[1].Value).Value);
        var list = Assert.IsType<NbtList>(decoded.Tags[2].Value);
        Assert.Equal(NbtTagType.DoubleTag, list.ElementType);
        Assert.Equal(3, list.Values.Count);
        Assert.Equal(-2.25, Assert.IsType<NbtDouble>(list.Values[1]).Value);
        Assert.Equal(_expectedIds, Assert.IsType<NbtIntArray>(decoded.Tags[3].Value).Value);
    }

    [Fact]
    public void NestedCompoundAndListsRoundTrip()
    {
        var inner = new NbtCompound([new NbtNamedTag("name", new NbtString("A"))]);
        var outer = new NbtCompound(
        [
            new NbtNamedTag("list", new NbtList(NbtTagType.CompoundTag, [inner, inner])),
            new NbtNamedTag("bytes", new NbtByteArray([1, -2, 127])),
            new NbtNamedTag("longs", new NbtLongArray([long.MaxValue, -1])),
            new NbtNamedTag("flag", new NbtByte(1)),
        ]);

        var writer = new PacketWriter();
        NbtWriter.WriteRoot(writer, outer);

        var reader = new PacketReader(writer.ToArray());
        var decoded = NbtReader.ReadRoot(ref reader);
        Assert.Equal(0, reader.Remaining);

        var list = Assert.IsType<NbtList>(decoded.Tags[0].Value);
        Assert.Equal(NbtTagType.CompoundTag, list.ElementType);
        Assert.Equal(2, list.Values.Count);
        Assert.Equal("A", Assert.IsType<NbtString>(
            Assert.IsType<NbtCompound>(list.Values[0]).Tags[0].Value).Value);

        Assert.Equal([(sbyte)1, (sbyte)-2, (sbyte)127],
            Assert.IsType<NbtByteArray>(decoded.Tags[1].Value).Value);
        Assert.Equal([long.MaxValue, -1L],
            Assert.IsType<NbtLongArray>(decoded.Tags[2].Value).Value);
        Assert.Equal(1, Assert.IsType<NbtByte>(decoded.Tags[3].Value).Value);
    }

    private static readonly byte[] _emptyCompound = { 0x0A, 0x00 };
    private static readonly int[] _expectedIds = { 1, 2, 3 };

    [Fact]
    public void EmptyCompoundRoundTrips()
    {
        var root = new NbtCompound([]);

        var writer = new PacketWriter();
        NbtWriter.WriteRoot(writer, root);

        // Network NBT empty compound: 0x0A then 0x00 (TAG_End).
        Assert.Equal(_emptyCompound, writer.ToArray());

        var reader = new PacketReader(writer.ToArray());
        var decoded = NbtReader.ReadRoot(ref reader);
        Assert.Empty(decoded.Tags);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void TruncatedRootThrows()
    {
        bool caught = false;
        try
        {
            var reader = new PacketReader(new byte[] { 0x0A });
            NbtReader.ReadRoot(ref reader);
        }
        catch (InvalidDataException)
        {
            caught = true;
        }
        Assert.True(caught);
    }

    [Fact]
    public void NonCompoundRootThrows()
    {
        bool caught = false;
        try
        {
            var reader = new PacketReader(new byte[] { 0x08, 0x00, 0x01, (byte)'x' });
            NbtReader.ReadRoot(ref reader);
        }
        catch (InvalidDataException)
        {
            caught = true;
        }
        Assert.True(caught);
    }

    [Fact]
    public void UnknownTagTypeThrows()
    {
        bool caught = false;
        try
        {
            var reader = new PacketReader(new byte[] { 0x0A, 0x0D });
            NbtReader.ReadRoot(ref reader);
        }
        catch (InvalidDataException)
        {
            caught = true;
        }
        Assert.True(caught);
    }

    [Fact]
    public void TruncatedStringValueThrows()
    {
        var truncated = new byte[]
        {
            0x0A,
            0x08, 0x00, 0x04, (byte)'n', (byte)'a', (byte)'m', (byte)'e',
            0x00, 0x09,
            (byte)'B',
        };
        bool caught = false;
        try
        {
            var reader = new PacketReader(truncated);
            NbtReader.ReadRoot(ref reader);
        }
        catch (InvalidDataException)
        {
            caught = true;
        }
        Assert.True(caught);
    }
}
