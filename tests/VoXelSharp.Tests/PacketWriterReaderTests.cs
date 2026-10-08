using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class PacketWriterReaderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(25565)]
    [InlineData(2097151)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    public void VarIntRoundTrip(int value)
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteVarInt(value);

        var reader = new PacketReader(pool.Bytes);
        Assert.Equal(value, reader.ReadVarInt());
        Assert.True(reader.IsEmpty);
    }

    [Fact]
    public void VarIntWritesExactlyMinimumBytes()
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteVarInt(0);

        Assert.Equal(new byte[] { 0x00 }, pool.Bytes);
    }

    [Fact]
    public void WriteStringKnownBytes()
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteString("abc", 255);

        Assert.Equal(new byte[] { 0x03, (byte)'a', (byte)'b', (byte)'c' }, pool.Bytes);
    }

    [Fact]
    public void WriteEmptyStringIsZeroLength()
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteString("", 255);

        Assert.Equal(new byte[] { 0x00 }, pool.Bytes);
    }

    [Fact]
    public void WriteStringUtf8MultiByte()
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteString("\u00E9", 255);

        // 0xC3 0xA9 is U+00E9 in UTF-8; length prefix counts bytes (2).
        Assert.Equal(new byte[] { 0x02, 0xC3, 0xA9 }, pool.Bytes);
    }

    [Fact]
    public void WriteStringExceedingCodeUnitLimitThrows()
    {
        using var pool = new PacketWriterScope();

        Assert.Throws<ArgumentException>(() => pool.Writer.WriteString("abcd", 3));
    }

    [Fact]
    public void WriteStringSurrogatePairAtByteLimit()
    {
        // U+1F600 is one scalar value: 2 UTF-16 code units, 4 UTF-8 bytes.
        // maxLength 2 permits 2 code units and 2 * 3 = 6 bytes, so this must succeed.
        using var pool = new PacketWriterScope();

        pool.Writer.WriteString("\U0001F600", 2);

        Assert.Equal(new byte[] { 0x04, 0xF0, 0x9F, 0x98, 0x80 }, pool.Bytes);
    }

    [Fact]
    public void WriteStringNegativeMaxLengthThrows()
    {
        using var pool = new PacketWriterScope();

        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Writer.WriteString("x", -1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(255)]
    [InlineData(65535)]
    public void UnsignedShortRoundTrip(int value)
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteUnsignedShort((ushort)value);

        var reader = new PacketReader(pool.Bytes);
        Assert.Equal((ushort)value, reader.ReadUnsignedShort());
        Assert.True(reader.IsEmpty);
    }

    [Fact]
    public void UnsignedShortIsBigEndian()
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteUnsignedShort(0x63DD);

        Assert.Equal(new byte[] { 0x63, 0xDD }, pool.Bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(0x0123456789ABCDEF)]
    public void LongRoundTrip(long value)
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteLong(value);

        var reader = new PacketReader(pool.Bytes);
        Assert.Equal(value, reader.ReadLong());
        Assert.True(reader.IsEmpty);
    }

    [Fact]
    public void LongIsBigEndian()
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteLong(0x0123456789ABCDEF);

        Assert.Equal(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }, pool.Bytes);
    }

    [Fact]
    public void ReadStringRoundTrip()
    {
        using var pool = new PacketWriterScope();
        pool.Writer.WriteString("Hello, world!", 255);

        var reader = new PacketReader(pool.Bytes);
        Assert.Equal("Hello, world!", reader.ReadString(255));
        Assert.True(reader.IsEmpty);
    }

    [Fact]
    public void ReadStringRejectsDeclaredLengthBeyondByteLimit()
    {
        // Declared length 7 bytes exceeds maxLength 2 * 3 = 6.
        var input = new byte[] { 0x07, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67 };
        var reader = new PacketReader(input);

        AssertStringReadThrows(reader, 2);
    }

    [Fact]
    public void ReadStringRejectsNegativeLength()
    {
        var input = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F };
        var reader = new PacketReader(input);

        AssertStringReadThrows(reader, 255);
    }

    [Fact]
    public void ReadStringRejectsTruncatedBody()
    {
        // Declared 5 bytes but only 1 present.
        var input = new byte[] { 0x05, 0x61 };
        var reader = new PacketReader(input);

        AssertStringReadThrows(reader, 255);
    }

    [Fact]
    public void ReadStringRejectsInvalidUtf8()
    {
        // 0xC3 must be followed by a continuation byte; 0x28 is not one.
        var input = new byte[] { 0x02, 0xC3, 0x28 };
        var reader = new PacketReader(input);

        AssertStringReadThrows(reader, 255);
    }

    [Fact]
    public void ReadStringRejectsCodeUnitOverflow()
    {
        // 128 emoji (U+1F600) = 256 UTF-16 code units > 255 limit, but only 512 bytes <= 765.
        string value = string.Concat(Enumerable.Repeat("\U0001F600", 128));
        using var pool = new PacketWriterScope();
        pool.Writer.WriteString(value, 32767);

        var reader = new PacketReader(pool.Bytes);
        AssertStringReadThrows(reader, 255);
    }

    [Fact]
    public void ReadStringAcceptsBoundaryLength()
    {
        string value = new string('a', 255);
        using var pool = new PacketWriterScope();
        pool.Writer.WriteString(value, 32767);

        var reader = new PacketReader(pool.Bytes);
        Assert.Equal(value, reader.ReadString(255));
        Assert.True(reader.IsEmpty);
    }

    [Fact]
    public void ReadVarIntTruncatedThrows()
    {
        var reader = new PacketReader(new byte[] { 0x80 });

        AssertReadVarIntThrows(reader);
    }

    [Fact]
    public void ReadUnsignedShortTruncatedThrows()
    {
        var reader = new PacketReader(new byte[] { 0x01 });

        AssertReadUnsignedShortThrows(reader);
    }

    [Fact]
    public void ReadLongTruncatedThrows()
    {
        var reader = new PacketReader(new byte[] { 0x01, 0x02, 0x03 });

        AssertReadLongThrows(reader);
    }

    [Fact]
    public void ReaderReportsPositionAndRemaining()
    {
        var reader = new PacketReader(new byte[] { 0x01, 0x02, 0x03 });

        Assert.Equal(0, reader.Position);
        Assert.Equal(3, reader.Remaining);
        Assert.False(reader.IsEmpty);

        Assert.Equal(1, reader.ReadVarInt());

        Assert.Equal(1, reader.Position);
        Assert.Equal(2, reader.Remaining);
        Assert.False(reader.IsEmpty);
    }

    [Fact]
    public void TryReadStringReturnsFalseOnTruncation()
    {
        var reader = new PacketReader(new byte[] { 0x05, 0x61 });

        Assert.False(reader.TryReadString(255, out _));
    }

    /// <summary>
    /// <see cref="PacketReader"/> is a ref struct and cannot be captured in a lambda;
    /// these helpers drive the throwing reads directly.
    /// </summary>
    private static void AssertStringReadThrows(PacketReader reader, int maxLength)
    {
        InvalidDataException? thrown = null;
        try
        {
            reader.ReadString(maxLength);
        }
        catch (InvalidDataException exception)
        {
            thrown = exception;
        }

        Assert.NotNull(thrown);
    }

    private static void AssertReadVarIntThrows(PacketReader reader)
    {
        InvalidDataException? thrown = null;
        try
        {
            reader.ReadVarInt();
        }
        catch (InvalidDataException exception)
        {
            thrown = exception;
        }

        Assert.NotNull(thrown);
    }

    private static void AssertReadUnsignedShortThrows(PacketReader reader)
    {
        InvalidDataException? thrown = null;
        try
        {
            reader.ReadUnsignedShort();
        }
        catch (InvalidDataException exception)
        {
            thrown = exception;
        }

        Assert.NotNull(thrown);
    }

    private static void AssertReadLongThrows(PacketReader reader)
    {
        InvalidDataException? thrown = null;
        try
        {
            reader.ReadLong();
        }
        catch (InvalidDataException exception)
        {
            thrown = exception;
        }

        Assert.NotNull(thrown);
    }
}
