namespace VoXelSharp.Protocol;

/// <summary>
/// Minecraft protocol VarInt (variable-length signed 32-bit integer).
/// Wire facts per docs/PROTOCOL.md: maximum encoded length is 5 bytes; overflow is rejected.
/// </summary>
public static class VarInt
{
    /// <summary>Maximum number of bytes a VarInt may occupy on the wire.</summary>
    public const int MaxEncodedLength = 5;

    /// <summary>Any bits above position 3 are invalid in the final byte of a 5-byte VarInt.</summary>
    private const byte _finalByteInvalidBits = 0xF0;

    private const byte _continueBit = 0x80;
    private const byte _valueMask = 0x7F;

    /// <summary>Returns the number of bytes this value occupies when encoded, in the range 1..5.</summary>
    public static int GetEncodedLength(int value)
    {
        int length = 1;
        uint unsigned = (uint)value >> 7;
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
    public static int Write(int value, Span<byte> destination)
    {
        int length = GetEncodedLength(value);
        if (destination.Length < length)
        {
            throw new ArgumentException(
                $"Destination span is too small to hold a {length}-byte VarInt (has {destination.Length} bytes).",
                nameof(destination));
        }

        uint unsigned = (uint)value;
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
    /// Attempts to read a VarInt from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The bytes to read from.</param>
    /// <param name="value">The decoded value when this returns <see langword="true"/>.</param>
    /// <param name="bytesRead">The number of bytes consumed when this returns <see langword="true"/>; otherwise 0.</param>
    /// <returns>
    /// <see langword="true"/> when a complete VarInt was found;
    /// <see langword="false"/> when the input is truncated, exceeds the 5-byte limit, or uses
    /// bits beyond the signed 32-bit range in the final byte.
    /// </returns>
    public static bool TryRead(ReadOnlySpan<byte> source, out int value, out int bytesRead)
    {
        value = 0;
        bytesRead = 0;

        for (int i = 0; i < source.Length; i++)
        {
            byte current = source[i];

            if (i == MaxEncodedLength - 1)
            {
                // In the final byte only bits 0..3 are valid (they map to value bits 28..31).
                // A set continuation bit means a 6th byte would be required; higher value bits
                // would overflow the signed 32-bit range.
                if ((current & (_continueBit | _finalByteInvalidBits)) != 0)
                {
                    return false;
                }
            }

            value |= (current & _valueMask) << (7 * i);

            if ((current & _continueBit) == 0)
            {
                bytesRead = i + 1;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a VarInt from <paramref name="source"/>.
    /// </summary>
    /// <returns>The number of bytes consumed.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the input is truncated, exceeds the 5-byte limit, or uses bits beyond the
    /// signed 32-bit range in the final byte.
    /// </exception>
    public static int Read(ReadOnlySpan<byte> source, out int value)
    {
        if (TryRead(source, out value, out int bytesRead))
        {
            return bytesRead;
        }

        if (source.Length >= MaxEncodedLength && (source[MaxEncodedLength - 1] & (_continueBit | _finalByteInvalidBits)) != 0)
        {
            throw new InvalidDataException(
                $"VarInt exceeds the maximum encoded length of {MaxEncodedLength} bytes.");
        }

        throw new InvalidDataException("Truncated VarInt: the input ended before a terminating byte was read.");
    }
}
