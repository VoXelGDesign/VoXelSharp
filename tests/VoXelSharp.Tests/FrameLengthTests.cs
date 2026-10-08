using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class FrameLengthTests
{
    public static TheoryData<int, byte[]> KnownValues => new()
    {
        { 0, new byte[] { 0x00 } },
        { 1, new byte[] { 0x01 } },
        { 127, new byte[] { 0x7F } },
        { 128, new byte[] { 0x80, 0x01 } },
        { 255, new byte[] { 0xFF, 0x01 } },
        { 25565, new byte[] { 0xDD, 0xC7, 0x01 } },
        { 16384, new byte[] { 0x80, 0x80, 0x01 } },
        { 2097150, new byte[] { 0xFE, 0xFF, 0x7F } },
        { 2097151, new byte[] { 0xFF, 0xFF, 0x7F } },
    };

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void WriteProducesKnownBytes(int frameLength, byte[] expected)
    {
        Span<byte> destination = stackalloc byte[FrameLength.MaxEncodedLength];

        int written = FrameLength.Write(frameLength, destination);

        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, destination[..written].ToArray());
    }

    [Theory]
    [MemberData(nameof(KnownValues))]
    public void ReadDecodesKnownBytes(int expected, byte[] encoded)
    {
        int consumed = FrameLength.Read(encoded, out int frameLength);

        Assert.Equal(expected, frameLength);
        Assert.Equal(encoded.Length, consumed);
    }

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void ReadAfterWriteIsRoundTrip(int value)
    {
        Span<byte> encoded = stackalloc byte[FrameLength.MaxEncodedLength];
        int written = FrameLength.Write(value, encoded);

        int consumed = FrameLength.Read(encoded[..written], out int decoded);

        Assert.Equal(value, decoded);
        Assert.Equal(written, consumed);
    }

    [Theory]
    [MemberData(nameof(TruncatedInputs))]
    public void ReadTruncatedInputThrows(byte[] input)
    {
        Assert.Throws<InvalidDataException>(() => FrameLength.Read(input, out _));
    }

    [Theory]
    [MemberData(nameof(TruncatedInputs))]
    public void TryReadTruncatedInputReturnsFalse(byte[] input)
    {
        Assert.False(FrameLength.TryRead(input, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Theory]
    [MemberData(nameof(RejectedInputs))]
    public void ReadRejectedInputThrows(byte[] input)
    {
        Assert.Throws<InvalidDataException>(() => FrameLength.Read(input, out _));
    }

    [Theory]
    [MemberData(nameof(RejectedInputs))]
    public void TryReadRejectedInputReturnsFalse(byte[] input)
    {
        Assert.False(FrameLength.TryRead(input, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2097152)]
    [InlineData(int.MaxValue)]
    public void WriteOutOfRangeThrows(int frameLength)
    {
        byte[] destination = new byte[VarInt.MaxEncodedLength];

        Assert.Throws<ArgumentOutOfRangeException>(() => FrameLength.Write(frameLength, destination));
    }

    [Fact]
    public void WriteTooSmallBufferThrows()
    {
        byte[] destination = new byte[FrameLength.MaxEncodedLength - 1];

        Assert.Throws<ArgumentException>(() => FrameLength.Write(2097151, destination));
    }

    [Fact]
    public void ReadIgnoresTrailingBytes()
    {
        byte[] input = new byte[] { 0x01, 0xAA, 0xBB };

        int consumed = FrameLength.Read(input, out int frameLength);

        Assert.Equal(1, frameLength);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void TryReadIgnoresTrailingBytes()
    {
        byte[] input = new byte[] { 0x01, 0xAA, 0xBB };

        bool success = FrameLength.TryRead(input, out int frameLength, out int bytesRead);

        Assert.True(success);
        Assert.Equal(1, frameLength);
        Assert.Equal(1, bytesRead);
    }

    [Fact]
    public void TryReadEmptyInputReturnsFalse()
    {
        Assert.False(FrameLength.TryRead(ReadOnlySpan<byte>.Empty, out _, out int bytesRead));
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ReadEmptyInputThrows()
    {
        Assert.Throws<InvalidDataException>(() => FrameLength.Read(ReadOnlySpan<byte>.Empty, out _));
    }

    [Fact]
    public void MaxFrameLengthMatchesProtocolFact()
    {
        Assert.Equal(2_097_151, FrameLength.MaxFrameLength);
    }

    [Fact]
    public void MaxEncodedLengthIsThreeBytes()
    {
        Assert.Equal(3, FrameLength.MaxEncodedLength);
        Assert.Equal(
            FrameLength.MaxEncodedLength,
            VarInt.GetEncodedLength(FrameLength.MaxFrameLength));
    }

    public static TheoryData<int> RoundTripValues => new()
    {
        { 0 },
        { 1 },
        { 127 },
        { 128 },
        { 255 },
        { 25565 },
        { 16384 },
        { 2097150 },
        { 2097151 },
    };

    public static TheoryData<byte[]> TruncatedInputs => new()
    {
        { new byte[] { 0x80 } },
        { new byte[] { 0x80, 0x80 } },
        { new byte[] { 0xFF, 0xFF } },
    };

    public static TheoryData<byte[]> RejectedInputs => new()
    {
        { new byte[] { 0x80, 0x80, 0x80, 0x01 } },
        { new byte[] { 0xFF, 0xFF, 0x80, 0x01 } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0x7F } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07 } },
        { new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F } },
        { new byte[] { 0x80, 0x80, 0x80, 0x00 } },
    };
}
