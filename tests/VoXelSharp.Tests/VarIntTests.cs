using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class VarIntTests
{
    public static TheoryData<int, byte[]> KnownValues => new()
    {
        { 0, new byte[] { 0x00 } },
        { 1, new byte[] { 0x01 } },
        { 127, new byte[] { 0x7F } },
        { 128, new byte[] { 0x80, 0x01 } },
        { 255, new byte[] { 0xFF, 0x01 } },
        { 25565, new byte[] { 0xDD, 0xC7, 0x01 } },
        { 2097151, new byte[] { 0xFF, 0xFF, 0x7F } },
        { 2097152, new byte[] { 0x80, 0x80, 0x80, 0x01 } },
        { int.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 } },
        { int.MinValue, new byte[] { 0x80, 0x80, 0x80, 0x80, 0x08 } },
        { -1, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F } },
    };

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void WriteProducesKnownBytes(int value, byte[] expected)
    {
        Span<byte> destination = stackalloc byte[VarInt.MaxEncodedLength];

        int written = VarInt.Write(value, destination);

        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, destination[..written].ToArray());
    }

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void ReadDecodesKnownBytes(int expected, byte[] encoded)
    {
        int consumed = VarInt.Read(encoded, out int value);

        Assert.Equal(expected, value);
        Assert.Equal(encoded.Length, consumed);
    }

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void ReadAfterWriteIsRoundTrip(int value)
    {
        Span<byte> encoded = stackalloc byte[VarInt.MaxEncodedLength];
        int written = VarInt.Write(value, encoded);

        int consumed = VarInt.Read(encoded[..written], out int decoded);

        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(127, 1)]
    [InlineData(128, 2)]
    [InlineData(255, 2)]
    [InlineData(2097151, 3)]
    [InlineData(2097152, 4)]
    [InlineData(int.MaxValue, 5)]
    [InlineData(int.MinValue, 5)]
    [InlineData(-1, 5)]
    public void GetEncodedLengthIsMinimumRequired(int value, int expected)
    {
        Assert.Equal(expected, VarInt.GetEncodedLength(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(1279432)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void WriteTooSmallBufferThrows(int value)
    {
        byte[] destination = new byte[VarInt.GetEncodedLength(value) - 1];

        Assert.Throws<ArgumentException>(() => VarInt.Write(value, destination));
    }

    [Theory]
    [MemberData(nameof(TruncatedInputs))]
    public void ReadTruncatedInputThrows(byte[] input)
    {
        Assert.Throws<InvalidDataException>(() => VarInt.Read(input, out _));
    }

    [Theory]
    [MemberData(nameof(TruncatedInputs))]
    public void TryReadTruncatedInputReturnsFalse(byte[] input)
    {
        Assert.False(VarInt.TryRead(input, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Theory]
    [MemberData(nameof(OverflowInputs))]
    public void ReadOverflowThrows(byte[] input)
    {
        Assert.Throws<InvalidDataException>(() => VarInt.Read(input, out _));
    }

    [Theory]
    [MemberData(nameof(OverflowInputs))]
    public void TryReadOverflowReturnsFalse(byte[] input)
    {
        Assert.False(VarInt.TryRead(input, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ReadIgnoresTrailingBytes()
    {
        byte[] input = new byte[] { 0x01, 0xAA, 0xBB };

        int consumed = VarInt.Read(input, out int value);

        Assert.Equal(1, value);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void TryReadIgnoresTrailingBytes()
    {
        byte[] input = new byte[] { 0x01, 0xAA, 0xBB };

        bool success = VarInt.TryRead(input, out int value, out int bytesRead);

        Assert.True(success);
        Assert.Equal(1, value);
        Assert.Equal(1, bytesRead);
    }

    [Fact]
    public void TryReadEmptyInputReturnsFalse()
    {
        Assert.False(VarInt.TryRead(ReadOnlySpan<byte>.Empty, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ReadEmptyInputThrows()
    {
        Assert.Throws<InvalidDataException>(() => VarInt.Read(ReadOnlySpan<byte>.Empty, out _));
    }

    public static TheoryData<byte[]> TruncatedInputs => new()
    {
        { new byte[] { 0x80 } },
        { new byte[] { 0x80, 0x80 } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF } },
    };

    public static TheoryData<int> RoundTripValues => new()
    {
        { 0 },
        { 1 },
        { 127 },
        { 128 },
        { 255 },
        { 25565 },
        { 2097151 },
        { 2097152 },
        { int.MaxValue },
        { int.MinValue },
        { -1 },
    };

    public static TheoryData<byte[]> OverflowInputs => new()
    {
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF } },
        { new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80 } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x7F } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x10 } },
    };
}
