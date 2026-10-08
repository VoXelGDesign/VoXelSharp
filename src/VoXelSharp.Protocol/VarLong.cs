namespace VoXelSharp.Protocol;

/// <summary>
/// Minecraft protocol VarLong (variable-length signed 64-bit integer).
/// Wire facts per docs/PROTOCOL.md: maximum encoded length is 10 bytes; overflow is rejected.
/// </summary>
public static class VarLong
{
    /// <summary>Maximum number of bytes a VarLong may occupy on the wire.</summary>
    public const int MaxEncodedLength = 10;

    /// <summary>Any bits above position 0 are invalid in the final byte of a 10-byte VarLong.</summary>
    private const byte _finalByteInvalidBits = 0xFE;

    private const byte _continueBit = 0x80;
    private const byte _valueMask = 0x7F;

    /// <summary>Returns the number of bytes this value occupies when encoded, in the range 1..10.</summary>
    public static int GetEncodedLength(long value)
    {
        int length = 1;
        ulong unsigned = (ulong)value >> 7;
        while (unsigned != 0)
        {
            length++;
            unsigned >>= 7;
        }

        return length;
    }

    /// <summary>
    /// Writes <paramref name="value"/> to <paramref name="destination"/>.
    /// </summary>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is too small.</exception>
    public static int Write(long value, Span<byte> destination)
    {
        int length = GetEncodedLength(value);
        if (destination.Length < length)
        {
            throw new ArgumentException(
                $"Destination span is too small to hold a {length}-byte VarLong (has {destination.Length} bytes).",
                nameof(destination));
        }

        ulong unsigned = (ulong)value;
        for (int i = 0; i < length; i++)
        {
            byte current = (byte)(unsigned & _valueMask);
            if (i < length - 1)
            {
                current |= _continueBit;
            }

            destination[i] = current;
            unsigned >>= 7;
        }

        return length;
    }

    /// <summary>
    /// Attempts to read a VarLong from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="value">The decoded value when this returns <see langword="true"/>.</param>
    /// <param name="bytesRead">The number of bytes consumed when this returns <see langword="true"/>; otherwise 0.</param>
    /// <returns>
    /// <see langword="true"/> when a complete VarLong was found;
    /// <see langword="false"/> when the input is truncated, exceeds the 10-byte limit, or uses
    /// bits beyond the signed 64-bit range in the final byte.
    /// </returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out long value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;

        for (int i = 0; i < source.Length; i++)
        {
            byte current = source[i];

            if (i == MaxEncodedLength - 1)
            {
                // The first 9 bytes supply 63 bits; the final byte supplies only bit 63.
                // A set continuation bit means an 11th byte would be required; higher value bits
                // would overflow the signed 64-bit range.
                if ((current & (_continueBit | _finalByteInvalidBits)) != 0)
                {
                    return false;
                }
            }

            value |= ((long)(current & _valueMask)) << (7 * i);

            if ((current & _continueBit) == 0)
            {
                bytesRead = i + 1;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a VarLong from <paramref name="source"/>.
    /// </summary>
    /// <returns>The number of bytes consumed.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the input is truncated, exceeds the 10-byte limit, or uses bits beyond the
    /// signed 64-bit range in the final byte.
    /// </exception>
    public static int Read(ReadOnlySpan<byte> source, out long value)
    {
        if (TryRead(source, out value, out int bytesRead))
        {
            return bytesRead;
        }

        if (source.Length >= MaxEncodedLength && (source[MaxEncodedLength - 1] & (_continueBit | _finalByteInvalidBits)) != 0)
        {
            throw new InvalidDataException(
                $"VarLong exceeds the maximum encoded length of {MaxEncodedLength} bytes.");
        }

        throw new InvalidDataException("Truncated VarLong: the input ended before a terminating byte was read.");
    }
}
