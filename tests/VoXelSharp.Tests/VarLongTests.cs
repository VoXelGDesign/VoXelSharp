using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class VarLongTests
{
    public static TheoryData<long, byte[]> KnownValues => new()
    {
        { 0L, new byte[] { 0x00 } },
        { 1L, new byte[] { 0x01 } },
        { 127L, new byte[] { 0x7F } },
        { 128L, new byte[] { 0x80, 0x01 } },
        { 255L, new byte[] { 0xFF, 0x01 } },
        { 25565L, new byte[] { 0xDD, 0xC7, 0x01 } },
        { 2097151L, new byte[] { 0xFF, 0xFF, 0x7F } },
        { 2097152L, new byte[] { 0x80, 0x80, 0x80, 0x01 } },
        { 34359738367L, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x7F } },
        { long.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F } },
        { long.MinValue, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 } },
        { -1L, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 } },
    };

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void WriteProducesKnownBytes(long value, byte[] expected)
    {
        Span<byte> destination = stackalloc byte[VarLong.MaxEncodedLength];

        int written = VarLong.Write(value, destination);

        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, destination[..written].ToArray());
    }

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void ReadDecodesKnownBytes(long expected, byte[] encoded)
    {
        int consumed = VarLong.Read(encoded, out long value);

        Assert.Equal(expected, value);
        Assert.Equal(encoded.Length, consumed);
    }

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void ReadAfterWriteIsRoundTrip(long value)
    {
        Span<byte> encoded = stackalloc byte[VarLong.MaxEncodedLength];
        int written = VarLong.Write(value, encoded);

        int consumed = VarLong.Read(encoded[..written], out long decoded);

        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Theory]
    [InlineData(0L, 1)]
    [InlineData(1L, 1)]
    [InlineData(127L, 1)]
    [InlineData(128L, 2)]
    [InlineData(255L, 2)]
    [InlineData(2097151L, 3)]
    [InlineData(2097152L, 4)]
    [InlineData(34359738367L, 5)]
    [InlineData(long.MaxValue, 9)]
    [InlineData(long.MinValue, 10)]
    [InlineData(-1L, 10)]
    public void GetEncodedLengthIsMinimumRequired(long value, int expected)
    {
        Assert.Equal(expected, VarLong.GetEncodedLength(value));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(127L)]
    [InlineData(128L)]
    [InlineData(127943235123L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void WriteTooSmallBufferThrows(long value)
    {
        byte[] destination = new byte[VarLong.GetEncodedLength(value) - 1];

        Assert.Throws<ArgumentException>(() => VarLong.Write(value, destination));
    }

    [Theory]
    [MemberData(nameof(TruncatedInputs))]
    public void ReadTruncatedInputThrows(byte[] input)
    {
        Assert.Throws<InvalidDataException>(() => VarLong.Read(input, out _));
    }

    [Theory]
    [MemberData(nameof(TruncatedInputs))]
    public void TryReadTruncatedInputReturnsFalse(byte[] input)
    {
        Assert.False(VarLong.TryRead(input, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Theory]
    [MemberData(nameof(OverflowInputs))]
    public void ReadOverflowThrows(byte[] input)
    {
        Assert.Throws<InvalidDataException>(() => VarLong.Read(input, out _));
    }

    [Theory]
    [MemberData(nameof(OverflowInputs))]
    public void TryReadOverflowReturnsFalse(byte[] input)
    {
        Assert.False(VarLong.TryRead(input, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ReadIgnoresTrailingBytes()
    {
        byte[] input = new byte[] { 0x01, 0xAA, 0xBB };

        int consumed = VarLong.Read(input, out long value);

        Assert.Equal(1, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void TryReadIgnoresTrailingBytes()
    {
        byte[] input = new byte[] { 0x01, 0xAA, 0xBB };

        bool success = VarLong.TryRead(input, out long value, out int bytesRead);

        Assert.True(success);
        Assert.Equal(1, value);
        Assert.Equal(1, bytesRead);
    }

    [Fact]
    public void TryReadEmptyInputReturnsFalse()
    {
        Assert.False(VarLong.TryRead(ReadOnlySpan<byte>.Empty, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ReadEmptyInputThrows()
    {
        Assert.Throws<InvalidDataException>(() => VarLong.Read(ReadOnlySpan<byte>.Empty, out _));
    }

    public static TheoryData<byte[]> TruncatedInputs => new()
    {
        { new byte[] { 0x80 } },
        { new byte[] { 0x80, 0x80 } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF } },
    };

    public static TheoryData<long> RoundTripValues => new()
    {
        { 0L },
        { 1L },
        { 127L },
        { 128L },
        { 255L },
        { 25565L },
        { 2097151L },
        { 2097152L },
        { 34359738367L },
        { long.MaxValue },
        { long.MinValue },
        { -1L },
    };

    public static TheoryData<byte[]> OverflowInputs => new()
    {
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF } },
        { new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02 } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F } },
    };
}
