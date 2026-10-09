using System.Text;

namespace VoXelSharp.Protocol.Nbt;

/// <summary>
/// Reads NBT in the Java Edition big-endian format. For Network NBT (protocol &gt;= 764) a root
/// compound is read from the initial 0x0A byte with no name-length prefix (docs/data/wire_types.md).
/// </summary>
public static class NbtReader
{
    private static readonly UTF8Encoding _modifiedUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads a Network NBT root compound: a Compound type byte (0x0A) followed by its payload.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown on truncated or malformed data.</exception>
    public static NbtCompound ReadRoot(ref PacketReader reader)
    {
        NbtTagType type = ReadType(ref reader);
        if (type != NbtTagType.CompoundTag)
        {
            throw new InvalidDataException($"NBT root must be a compound, but found type {type}.");
        }

        return ReadCompoundPayload(ref reader);
    }

    private static NbtTagType ReadType(ref PacketReader reader)
    {
        byte value = reader.ReadByte();
        if (value > (byte)NbtTagType.LongArrayTag)
        {
            throw new InvalidDataException($"Unknown NBT tag type 0x{value:X2}.");
        }

        return (NbtTagType)value;
    }

    private static NbtCompound ReadCompoundPayload(ref PacketReader reader)
    {
        var tags = new List<NbtNamedTag>();
        while (true)
        {
            NbtTagType type = ReadType(ref reader);
            if (type == NbtTagType.End)
            {
                break;
            }

            string name = ReadModifiedUtf8(ref reader);
            NbtNode value = ReadPayload(ref reader, type);
            tags.Add(new NbtNamedTag(name, value));
        }

        return new NbtCompound(tags);
    }

    private static NbtNode ReadPayload(ref PacketReader reader, NbtTagType type)
    {
        switch (type)
        {
            case NbtTagType.ByteTag:
                return new NbtByte((sbyte)reader.ReadByte());
            case NbtTagType.ShortTag:
                return new NbtShort(reader.ReadShort());
            case NbtTagType.IntTag:
                return new NbtInt(reader.ReadInt());
            case NbtTagType.LongTag:
                return new NbtLong(reader.ReadLong());
            case NbtTagType.FloatTag:
                return new NbtFloat(reader.ReadFloat());
            case NbtTagType.DoubleTag:
                return new NbtDouble(reader.ReadDouble());
            case NbtTagType.ByteArrayTag:
                return ReadByteArray(ref reader);
            case NbtTagType.StringTag:
                return new NbtString(ReadModifiedUtf8(ref reader));
            case NbtTagType.ListTag:
                return ReadList(ref reader);
            case NbtTagType.CompoundTag:
                return ReadCompoundPayload(ref reader);
            case NbtTagType.IntArrayTag:
                return ReadIntArray(ref reader);
            case NbtTagType.LongArrayTag:
                return ReadLongArray(ref reader);
            case NbtTagType.End:
            default:
                throw new InvalidDataException($"Tag type {type} is not a valid payload type.");
        }
    }

    private static NbtByteArray ReadByteArray(ref PacketReader reader)
    {
        int length = reader.ReadInt();
        if (length < 0)
        {
            throw new InvalidDataException($"Negative NBT byte-array length ({length}).");
        }

        var values = new sbyte[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = (sbyte)reader.ReadByte();
        }

        return new NbtByteArray(values);
    }

    private static NbtIntArray ReadIntArray(ref PacketReader reader)
    {
        int length = reader.ReadInt();
        if (length < 0)
        {
            throw new InvalidDataException($"Negative NBT int-array length ({length}).");
        }

        var values = new int[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = reader.ReadInt();
        }

        return new NbtIntArray(values);
    }

    private static NbtLongArray ReadLongArray(ref PacketReader reader)
    {
        int length = reader.ReadInt();
        if (length < 0)
        {
            throw new InvalidDataException($"Negative NBT long-array length ({length}).");
        }

        var values = new long[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = reader.ReadLong();
        }

        return new NbtLongArray(values);
    }

    private static NbtList ReadList(ref PacketReader reader)
    {
        NbtTagType elementType = ReadType(ref reader);
        if (elementType == NbtTagType.End)
        {
            throw new InvalidDataException("NBT list element type cannot be TAG_End.");
        }

        int length = reader.ReadInt();
        if (length < 0)
        {
            throw new InvalidDataException($"Negative NBT list length ({length}).");
        }

        var values = new List<NbtNode>(length);
        for (int i = 0; i < length; i++)
        {
            values.Add(ReadPayload(ref reader, elementType));
        }

        return new NbtList(elementType, values);
    }

    /// <summary>
    /// Reads a modified UTF-8 string prefixed by an unsigned short byte length.
    /// </summary>
    private static string ReadModifiedUtf8(ref PacketReader reader)
    {
        ushort byteLength = reader.ReadUnsignedShort();
        byte[] bytes = reader.ReadBytes(byteLength);
        try
        {
            return _modifiedUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("NBT string contains invalid modified UTF-8.", exception);
        }
    }
}
