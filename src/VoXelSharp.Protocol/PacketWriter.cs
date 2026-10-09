using System.Buffers.Binary;
using System.Text;

namespace VoXelSharp.Protocol;

/// <summary>
/// Growable writer for packet payloads (the packet ID VarInt followed by the packet fields).
/// Wire facts per the Minecraft Wiki "Data types" article: every multi-byte integer except
/// VarInt/VarLong is big-endian; a String is UTF-8 prefixed by its length in bytes as a VarInt,
/// limited to <c>maxLength</c> UTF-16 code units and <c>maxLength * 3</c> bytes.
/// </summary>
public sealed class PacketWriter
{
    private byte[] _buffer;
    private int _position;

    /// <summary>Creates a writer with an unspecified initial capacity.</summary>
    public PacketWriter()
        : this(64)
    {
    }

    /// <summary>Creates a writer whose buffer starts with the given capacity.</summary>
    public PacketWriter(int initialCapacity)
    {
        _buffer = new byte[Math.Max(initialCapacity, 1)];
        _position = 0;
    }

    /// <summary>Number of bytes written so far.</summary>
    public int Position => _position;

    /// <summary>The bytes written so far.</summary>
    public Span<byte> WrittenSpan => _buffer.AsSpan(0, _position);

    /// <summary>Returns a copy of the bytes written so far.</summary>
    public byte[] ToArray() => WrittenSpan.ToArray();

    /// <summary>
    /// Writes a VarInt.
    /// </summary>
    public void WriteVarInt(int value)
    {
        int encodedLength = VarInt.GetEncodedLength(value);
        EnsureCapacity(encodedLength);
        VarInt.Write(value, _buffer.AsSpan(_position));
        _position += encodedLength;
    }

    /// <summary>
    /// Writes a length-prefixed UTF-8 string.
    /// </summary>
    /// <param name="value">The string to write.</param>
    /// <param name="maxLength">Maximum number of UTF-16 code units; the encoded string must use
    /// at most <c>maxLength * 3</c> bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxLength"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> exceeds <paramref name="maxLength"/> UTF-16 code units
    /// or encodes to more than <c>maxLength * 3</c> bytes.
    /// </exception>
    public void WriteString(string value, int maxLength)
    {
        if (maxLength < 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"String of {value.Length} UTF-16 code units exceeds the limit of {maxLength}.",
                nameof(value));
        }

        int byteCount = Encoding.UTF8.GetByteCount(value);
        if ((long)byteCount > (long)maxLength * 3)
        {
            throw new ArgumentException(
                $"String encodes to {byteCount} bytes, exceeding the {maxLength * 3}-byte limit for maxLength {maxLength}.",
                nameof(value));
        }

        WriteVarInt(byteCount);
        EnsureCapacity(byteCount);
        Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_position));
        _position += byteCount;
    }

    /// <summary>
    /// Writes an unsigned 16-bit big-endian integer.
    /// </summary>
    public void WriteUnsignedShort(ushort value)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteUInt16BigEndian(_buffer.AsSpan(_position), value);
        _position += 2;
    }

    /// <summary>
    /// Writes a single byte.
    /// </summary>
    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_position] = value;
        _position += 1;
    }

    /// <summary>
    /// Writes an exact sequence of bytes.
    /// </summary>
    public void WriteBytes(ReadOnlySpan<byte> data)
    {
        int count = data.Length;
        EnsureCapacity(count);
        data.CopyTo(_buffer.AsSpan(_position));
        _position += count;
    }

    /// <summary>
    /// Writes a 1-byte Boolean: 0x01 for true, 0x00 for false.
    /// </summary>
    public void WriteBoolean(bool value)
    {
        EnsureCapacity(1);
        _buffer[_position] = (byte)(value ? 0x01 : 0x00);
        _position += 1;
    }

    /// <summary>
    /// Writes a signed 64-bit big-endian integer.
    /// </summary>
    public void WriteLong(long value)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteInt64BigEndian(_buffer.AsSpan(_position), value);
        _position += 8;
    }

    /// <summary>
    /// Writes a signed 16-bit big-endian integer.
    /// </summary>
    public void WriteShort(short value)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteInt16BigEndian(_buffer.AsSpan(_position), value);
        _position += 2;
    }

    /// <summary>
    /// Writes a signed 32-bit big-endian integer.
    /// </summary>
    public void WriteInt(int value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteInt32BigEndian(_buffer.AsSpan(_position), value);
        _position += 4;
    }

    /// <summary>
    /// Writes a block position as a packed 64-bit big-endian integer
    /// (docs/data/wire_types.md: x 26 bits, z 26 bits, y 12 bits).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any coordinate is outside the network Position ranges.
    /// </exception>
    public void WritePosition(BlockPosition position)
    {
        if (position.X < BlockPosition.MinHorizontalCoordinate || position.X > BlockPosition.MaxHorizontalCoordinate
            || position.Z < BlockPosition.MinHorizontalCoordinate || position.Z > BlockPosition.MaxHorizontalCoordinate
            || position.Y < BlockPosition.MinVerticalCoordinate || position.Y > BlockPosition.MaxVerticalCoordinate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                $"Position ({position.X}, {position.Y}, {position.Z}) is outside the network ranges.");
        }

        WriteLong(position.ToPacked());
    }

    /// <summary>
    /// Writes an IEEE-754 single-precision big-endian float.
    /// </summary>
    public void WriteFloat(float value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteSingleBigEndian(_buffer.AsSpan(_position), value);
        _position += 4;
    }

    /// <summary>
    /// Writes an IEEE-754 double-precision big-endian float.
    /// </summary>
    public void WriteDouble(double value)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteDoubleBigEndian(_buffer.AsSpan(_position), value);
        _position += 8;
    }

    private void EnsureCapacity(int additionalBytes)
    {
        int required = _position + additionalBytes;
        if (required <= _buffer.Length)
        {
            return;
        }

        // Grow geometrically to keep repeated appends amortized O(1).
        int newSize = Math.Max(required, _buffer.Length * 2);
        Array.Resize(ref _buffer, newSize);
    }
}
