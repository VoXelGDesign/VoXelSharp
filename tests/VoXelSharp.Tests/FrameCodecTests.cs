using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class FrameCodecTests
{
    [Fact]
    public void EmptyInputIsNotAFrame()
    {
        Assert.False(FrameCodec.TryReadFrame(ReadOnlySpan<byte>.Empty, out _, out _));
    }

    [Fact]
    public void ZeroLengthFrameDecodesToEmptyPayload()
    {
        Assert.True(FrameCodec.TryReadFrame(new byte[] { 0x00 }, out byte[] payload, out int consumed));

        Assert.Empty(payload);
        Assert.Equal(1, consumed);
    }

    [Fact]
    public void OneBytePayloadFrameDecodes()
    {
        Assert.True(FrameCodec.TryReadFrame(new byte[] { 0x01, 0xAA }, out byte[] payload, out int consumed));

        Assert.Equal(new byte[] { 0xAA }, payload);
        Assert.Equal(2, consumed);
    }

    [Fact]
    public void SingleByteLengthPrefixUpTo127Decodes()
    {
        // payload of 127 bytes → length prefix 0x7F.
        byte[] frame = [0x7F, .. Enumerable.Range(0, 127).Select(i => (byte)i)];
        Assert.True(FrameCodec.TryReadFrame(frame, out byte[] payload, out int consumed));

        Assert.Equal(127, payload.Length);
        Assert.Equal(1 + 127, consumed);
    }

    [Fact]
    public void TwoByteLengthPrefixDecodes()
    {
        // payload of 128 bytes → length prefix 0x80 0x01.
        byte[] frame = [0x80, 0x01, .. Enumerable.Range(0, 128).Select(i => (byte)i)];
        Assert.True(FrameCodec.TryReadFrame(frame, out byte[] payload, out int consumed));

        Assert.Equal(128, payload.Length);
        Assert.Equal(2 + 128, consumed);
    }

    [Fact]
    public void MaximumLengthPrefixRequiresMoreBytes()
    {
        // 2,097,151 = 0xFF 0xFF 0x7F is the maximum 3-byte VarInt frame length;
        // with no body bytes the frame is incomplete, not invalid.
        Assert.False(FrameCodec.TryReadFrame(new byte[] { 0xFF, 0xFF, 0x7F }, out _, out _));
    }

    [Fact]
    public void MaximumValidFrameDecodes()
    {
        byte[] frame = new byte[3 + FrameLength.MaxFrameLength];
        frame[0] = 0xFF;
        frame[1] = 0xFF;
        frame[2] = 0x7F;
        frame[3] = 0xAB;

        Assert.True(FrameCodec.TryReadFrame(frame, out byte[] payload, out int consumed));

        Assert.Equal(FrameLength.MaxFrameLength, payload.Length);
        Assert.Equal(0xAB, payload[0]);
        Assert.Equal(frame.Length, consumed);
    }

    [Fact]
    public void PrefixWiderThanThreeBytesIsRejected()
    {
        // 2,097,152 needs a 4-byte VarInt (0x80 0x80 0x80 0x01); the prefix itself is illegal.
        var input = new byte[] { 0x80, 0x80, 0x80, 0x01 };

        Assert.Throws<InvalidDataException>(() => FrameCodec.TryReadFrame(input, out _, out _));
    }

    [Fact]
    public void InRangeValueInFourBytePrefixIsRejected()
    {
        // 0x80 0x80 0x80 0x00 is a 4-byte encoding of length 0 — the value fits, but the prefix
        // is longer than the allowed 3 bytes, so it must be rejected (wiki: "must not be longer
        // than 3 bytes, even if the encoded value is within the limit").
        var input = new byte[] { 0x80, 0x80, 0x80, 0x00 };

        Assert.Throws<InvalidDataException>(() => FrameCodec.TryReadFrame(input, out _, out _));
    }

    [Fact]
    public void ContinuationOnlyThreeBytePrefixIsRejected()
    {
        // Three continuation bytes can never terminate a length prefix (max 3 bytes).
        var input = new byte[] { 0x80, 0x80, 0x80 };

        Assert.Throws<InvalidDataException>(() => FrameCodec.TryReadFrame(input, out _, out _));
    }

    [Fact]
    public void TruncatedPrefixReturnsFalse()
    {
        Assert.False(FrameCodec.TryReadFrame(new byte[] { 0x80 }, out _, out _));
    }

    [Fact]
    public void TruncatedBodyReturnsFalse()
    {
        // Declares length 5 but provides only 2 body bytes.
        Assert.False(FrameCodec.TryReadFrame(new byte[] { 0x05, 0x01, 0x02 }, out _, out _));
    }

    [Fact]
    public void UnnecessarilyLongEncodingWithinThreeBytesIsAllowed()
    {
        // 0x81 0x00 is a legal 2-byte encoding of length 1 (wiki: over-long encodings of at most
        // 3 bytes are allowed).
        var frame = new byte[] { 0x81, 0x00, 0xAA };

        Assert.True(FrameCodec.TryReadFrame(frame, out byte[] payload, out int consumed));

        Assert.Equal(new byte[] { 0xAA }, payload);
        Assert.Equal(3, consumed);
    }

    [Fact]
    public void ThreeByteOverlongEncodingIsAllowed()
    {
        // 0x81 0x80 0x00 is a legal 3-byte encoding of length 1; over-long encodings of at most
        // 3 bytes are allowed.
        var frame = new byte[] { 0x81, 0x80, 0x00, 0xAA };

        Assert.True(FrameCodec.TryReadFrame(frame, out byte[] payload, out int consumed));

        Assert.Equal(new byte[] { 0xAA }, payload);
        Assert.Equal(4, consumed);
    }

    [Fact]
    public void ConsecutiveFramesAreConsumedInOrder()
    {
        // Two frames: [len 2][0x01 0x02] then [len 1][0x03].
        byte[] buffer = { 0x02, 0x01, 0x02, 0x01, 0x03 };

        Assert.True(FrameCodec.TryReadFrame(buffer, out byte[] first, out int firstConsumed));
        Assert.Equal(new byte[] { 0x01, 0x02 }, first);
        Assert.Equal(3, firstConsumed);

        Assert.True(FrameCodec.TryReadFrame(buffer.AsSpan(firstConsumed), out byte[] second, out int secondConsumed));
        Assert.Equal(new byte[] { 0x03 }, second);
        Assert.Equal(2, secondConsumed);
    }

    [Fact]
    public void EncodeEmptyPayloadWritesZeroPrefix()
    {
        Span<byte> destination = stackalloc byte[4];

        int written = FrameCodec.EncodeFrame([], destination);

        Assert.Equal(1, written);
        Assert.Equal(new byte[] { 0x00 }, destination[..written].ToArray());
    }

    [Fact]
    public void EncodeWritesLengthPrefixAndPayload()
    {
        Span<byte> destination = stackalloc byte[16];

        int written = FrameCodec.EncodeFrame(new byte[] { 0x01, 0x02, 0x03 }, destination);

        Assert.Equal(new byte[] { 0x03, 0x01, 0x02, 0x03 }, destination[..written].ToArray());
    }

    [Fact]
    public void EncodeTwoByteLengthPrefix()
    {
        byte[] payload = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
        Span<byte> destination = stackalloc byte[128 + 3];

        int written = FrameCodec.EncodeFrame(payload, destination);

        Assert.Equal(0x80, destination[0]);
        Assert.Equal(0x01, destination[1]);
        Assert.Equal(128 + 2, written);
    }

    [Fact]
    public void EncodeDestinationTooSmallThrows()
    {
        byte[] destination = new byte[2];

        Assert.Throws<ArgumentException>(() => FrameCodec.EncodeFrame(new byte[] { 0x01, 0x02, 0x03 }, destination));
    }

    [Fact]
    public void EncodePayloadOverMaxThrows()
    {
        byte[] destination = new byte[FrameLength.MaxEncodedLength + 2];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => FrameCodec.EncodeFrame(new byte[FrameLength.MaxFrameLength + 1], destination));
    }

    [Fact]
    public void RoundTripEncodeDecodePreservesPayload()
    {
        byte[] payload = Enumerable.Range(0, 300).Select(i => (byte)(i * 7)).ToArray();
        byte[] frame = new byte[FrameLength.MaxEncodedLength + payload.Length];
        int written = FrameCodec.EncodeFrame(payload, frame);

        Assert.True(FrameCodec.TryReadFrame(frame.AsSpan(0, written), out byte[] decoded, out int consumed));

        Assert.Equal(payload, decoded);
        Assert.Equal(written, consumed);
    }
}
