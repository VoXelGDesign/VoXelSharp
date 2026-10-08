namespace VoXelSharp.Protocol;

/// <summary>
/// Minecraft protocol frame length prefix.
/// Wire facts per docs/PROTOCOL.md: the maximum frame length is 2,097,151 bytes (2^21 - 1)
/// and frames exceeding this limit must be rejected.
/// </summary>
public static class FrameLength
{
    /// <summary>Maximum length of a single frame, in bytes.</summary>
    public const int MaxFrameLength = 2_097_151;

    /// <summary>
    /// Maximum number of bytes the length prefix may occupy on the wire.
    /// Every value in range 0..<see cref="MaxFrameLength"/> fits in 21 bits, so the prefix
    /// never needs more than 3 bytes.
    /// </summary>
    public const int MaxEncodedLength = 3;

    /// <summary>
    /// Writes the frame length prefix for <paramref name="frameLength"/>.
    /// </summary>
    /// <param name="frameLength">The frame length, in the range 0..<see cref="MaxFrameLength"/>.</param>
    /// <param name="destination">The destination buffer, which must be large enough for the prefix.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="frameLength"/> is negative or exceeds <see cref="MaxFrameLength"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small.</exception>
    public static int Write(int frameLength, Span<byte> destination)
    {
        if ((uint)frameLength > MaxFrameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameLength),
                frameLength,
                $"Frame length must be in the range 0..{MaxFrameLength} bytes.");
        }

        return VarInt.Write(frameLength, destination);
    }

    /// <summary>
    /// Attempts to read a frame length prefix from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="frameLength">The decoded frame length when this returns <see langword="true"/>.</param>
    /// <param name="bytesRead">The number of bytes consumed when this returns <see langword="true"/>; otherwise 0.</param>
    /// <returns>
    /// <see langword="true"/> when a complete, valid frame length was found.
    /// <see langword="false"/> when the input is truncated, the prefix is wider than
    /// <see cref="MaxEncodedLength"/> bytes, or the value is outside 0..<see cref="MaxFrameLength"/>.
    /// </returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out int frameLength, out int bytesRead)
    {
        frameLength = 0;
        bytesRead = 0;

        if (!VarInt.TryRead(source, out int raw, out int rawBytesRead))
        {
            return false;
        }

        if (rawBytesRead > MaxEncodedLength || (uint)raw > MaxFrameLength)
        {
            return false;
        }

        frameLength = raw;
        bytesRead = rawBytesRead;
        return true;
    }

    /// <summary>
    /// Reads a frame length prefix from <paramref name="source"/>.
    /// </summary>
    /// <returns>The number of bytes consumed.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the input is truncated, the prefix is wider than <see cref="MaxEncodedLength"/> bytes,
    /// or the value is outside 0..<see cref="MaxFrameLength"/>.
    /// </exception>
    public static int Read(ReadOnlySpan<byte> source, out int frameLength)
    {
        if (TryRead(source, out frameLength, out int bytesRead))
        {
            return bytesRead;
        }

        if (VarInt.TryRead(source, out int raw, out int rawBytesRead))
        {
            if (rawBytesRead > MaxEncodedLength)
            {
                throw new InvalidDataException(
                    $"Frame length prefix is wider than {MaxEncodedLength} bytes (frames are limited to {MaxFrameLength} bytes).");
            }

            throw new InvalidDataException(
                $"Frame length {raw} is outside the valid range 0..{MaxFrameLength} bytes.");
        }

        throw new InvalidDataException("Truncated or malformed frame length prefix.");
    }
}
