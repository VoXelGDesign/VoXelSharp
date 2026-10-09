using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace VoXelSharp.Protocol;

/// <summary>
/// Sequential reader over a packet payload (the bytes after the frame and compression layers,
/// starting at the packet ID).
/// Wire facts per the Minecraft Wiki "Data types" article: every multi-byte integer except
/// VarInt/VarLong is big-endian; a String is UTF-8 prefixed by its length in bytes as a VarInt,
/// limited to <c>maxLength</c> UTF-16 code units and <c>maxLength * 3</c> bytes.
/// </summary>
public ref struct PacketReader
{
    private static readonly UTF8Encoding _strictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly ReadOnlySpan<byte> _data;
    private int _position;

    public PacketReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }

    /// <summary>Number of bytes consumed so far.</summary>
    public readonly int Position => _position;

    /// <summary>Number of unread bytes remaining.</summary>
    public readonly int Remaining => _data.Length - _position;

    /// <summary>True when the payload has been fully consumed.</summary>
    public readonly bool IsEmpty => _position >= _data.Length;

    /// <summary>
    /// Reads a VarInt.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the VarInt is truncated or overflows.</exception>
    public int ReadVarInt()
    {
        if (VarInt.TryRead(_data[_position..], out int value, out int bytesRead))
        {
            _position += bytesRead;
            return value;
        }

        throw new InvalidDataException("Truncated or malformed VarInt in packet payload.");
    }

    /// <summary>Attempts to read a VarInt; consumes no bytes on failure.</summary>
    public bool TryReadVarInt(out int value)
    {
        if (VarInt.TryRead(_data[_position..], out value, out int bytesRead))
        {
            _position += bytesRead;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Reads a length-prefixed UTF-8 string.
    /// </summary>
    /// <param name="maxLength">Maximum number of UTF-16 code units; the encoded string may use
    /// at most <c>maxLength * 3</c> bytes.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the string is truncated, has a negative or over-limit byte length, contains
    /// invalid UTF-8, or decodes to more than <paramref name="maxLength"/> code units.
    /// </exception>
    public string ReadString(int maxLength)
    {
        if (maxLength < 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        }

        if (!TryReadVarInt(out int byteLength))
        {
            throw new InvalidDataException("Truncated string length in packet payload.");
        }

        if (byteLength < 0)
        {
            throw new InvalidDataException($"Negative string length ({byteLength}) in packet payload.");
        }

        if (byteLength > (long)maxLength * 3)
        {
            throw new InvalidDataException(
                $"String byte length {byteLength} exceeds the {maxLength * 3}-byte limit for maxLength {maxLength}.");
        }

        if (byteLength > Remaining)
        {
            throw new InvalidDataException(
                $"String of {byteLength} bytes is truncated; only {Remaining} bytes remain in the payload.");
        }

        ReadOnlySpan<byte> utf8 = _data.Slice(_position, byteLength);
        string value;
        try
        {
            value = _strictUtf8.GetString(utf8);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("String contains invalid UTF-8.", exception);
        }

        _position += byteLength;

        if (value.Length > maxLength)
        {
            throw new InvalidDataException(
                $"String of {value.Length} UTF-16 code units exceeds the limit of {maxLength}.");
        }

        return value;
    }

    /// <summary>Attempts to read a length-prefixed UTF-8 string; consumes no bytes on failure.</summary>
    public bool TryReadString(int maxLength, [NotNullWhen(true)] out string? value)
    {
        int start = _position;
        try
        {
            value = ReadString(maxLength);
            return true;
        }
        catch (InvalidDataException)
        {
            _position = start;
            value = null;
            return false;
        }
    }

    /// <summary>
    /// Reads an unsigned 16-bit big-endian integer.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 2 bytes remain.</exception>
    public ushort ReadUnsignedShort()
    {
        if (Remaining < 2)
        {
            throw new InvalidDataException("Truncated unsigned short in packet payload.");
        }

        ushort value = BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(_position, 2));
        _position += 2;
        return value;
    }

    /// <summary>Attempts to read an unsigned 16-bit big-endian integer; consumes no bytes on failure.</summary>
    public bool TryReadUnsignedShort(out ushort value)
    {
        if (Remaining < 2)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(_data.Slice(_position, 2));
        _position += 2;
        return true;
    }

    /// <summary>
    /// Reads a 1-byte Boolean (0x01 = true, 0x00 = false).
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 1 byte remains, or the byte is not 0x00 or 0x01.</exception>
    public bool ReadBoolean()
    {
        if (Remaining < 1)
        {
            throw new InvalidDataException("Truncated boolean in packet payload.");
        }

        byte value = _data[_position];
        _position += 1;

        if (value == 0x01)
        {
            return true;
        }

        if (value == 0x00)
        {
            return false;
        }

        throw new InvalidDataException($"Invalid boolean value 0x{value:X} (expected 0x00 or 0x01).");
    }

    /// <summary>
    /// Reads a signed 64-bit big-endian integer.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 8 bytes remain.</exception>
    public long ReadLong()
    {
        if (Remaining < 8)
        {
            throw new InvalidDataException("Truncated long in packet payload.");
        }

        long value = BinaryPrimitives.ReadInt64BigEndian(_data.Slice(_position, 8));
        _position += 8;
        return value;
    }

    /// <summary>Attempts to read a signed 64-bit big-endian integer; consumes no bytes on failure.</summary>
    public bool TryReadLong(out long value)
    {
        if (Remaining < 8)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadInt64BigEndian(_data.Slice(_position, 8));
        _position += 8;
        return true;
    }

    /// <summary>
    /// Reads a single byte.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 1 byte remains.</exception>
    public byte ReadByte()
    {
        if (Remaining < 1)
        {
            throw new InvalidDataException("Truncated byte in packet payload.");
        }

        return _data[_position++];
    }

    /// <summary>
    /// Reads a BitSet: VarInt length in longs, then the longs (docs/data/wire_types.md).
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the payload is malformed or truncated.</exception>
    public long[] ReadBitSet()
    {
        int longCount = ReadVarInt();
        if (longCount < 0)
        {
            throw new InvalidDataException($"Negative BitSet length ({longCount}).");
        }

        if ((long)longCount * 8 > Remaining)
        {
            throw new InvalidDataException(
                $"BitSet of {longCount} longs is truncated; only {Remaining} bytes remain in the payload.");
        }

        var data = new long[longCount];
        for (int i = 0; i < longCount; i++)
        {
            data[i] = ReadLong();
        }

        return data;
    }

    /// <summary>
    /// Reads a length-prefixed (Prefixed Array) byte array: VarInt length then the bytes.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the payload is malformed or truncated.</exception>
    public byte[] ReadPrefixedByteArray()
    {
        int count = ReadVarInt();
        if (count < 0)
        {
            throw new InvalidDataException($"Negative prefixed byte-array length ({count}).");
        }

        return ReadBytes(count);
    }

    /// <summary>
    /// Reads a block position packed as a big-endian 64-bit integer.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 8 bytes remain.</exception>
    public BlockPosition ReadPosition()
    {
        if (Remaining < 8)
        {
            throw new InvalidDataException("Truncated position in packet payload.");
        }

        long packed = ReadLong();
        return BlockPosition.FromPacked(packed);
    }

    /// <summary>
    /// Reads a signed 16-bit big-endian integer.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 2 bytes remain.</exception>
    public short ReadShort()
    {
        if (Remaining < 2)
        {
            throw new InvalidDataException("Truncated short in packet payload.");
        }

        short value = BinaryPrimitives.ReadInt16BigEndian(_data.Slice(_position, 2));
        _position += 2;
        return value;
    }

    /// <summary>
    /// Reads a signed 32-bit big-endian integer.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 4 bytes remain.</exception>
    public int ReadInt()
    {
        if (Remaining < 4)
        {
            throw new InvalidDataException("Truncated int in packet payload.");
        }

        int value = BinaryPrimitives.ReadInt32BigEndian(_data.Slice(_position, 4));
        _position += 4;
        return value;
    }

    /// <summary>
    /// Reads an IEEE-754 single-precision big-endian float.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 4 bytes remain.</exception>
    public float ReadFloat()
    {
        if (Remaining < 4)
        {
            throw new InvalidDataException("Truncated float in packet payload.");
        }

        float value = BinaryPrimitives.ReadSingleBigEndian(_data.Slice(_position, 4));
        _position += 4;
        return value;
    }

    /// <summary>
    /// Reads an IEEE-754 double-precision big-endian float.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than 8 bytes remain.</exception>
    public double ReadDouble()
    {
        if (Remaining < 8)
        {
            throw new InvalidDataException("Truncated double in packet payload.");
        }

        double value = BinaryPrimitives.ReadDoubleBigEndian(_data.Slice(_position, 8));
        _position += 8;
        return value;
    }

    /// <summary>
    /// Reads a byte array of the given exact length.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when fewer than <paramref name="count"/> bytes remain.</exception>
    public byte[] ReadBytes(int count)
    {
        if (count < 0)
        {
            throw new InvalidDataException($"Negative byte count ({count}) in packet payload.");
        }

        if (count > Remaining)
        {
            throw new InvalidDataException(
                $"Byte array of {count} bytes is truncated; only {Remaining} bytes remain in the payload.");
        }

        byte[] value = _data.Slice(_position, count).ToArray();
        _position += count;
        return value;
    }
}
