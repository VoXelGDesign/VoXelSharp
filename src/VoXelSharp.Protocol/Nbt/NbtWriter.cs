using System.Text;

namespace VoXelSharp.Protocol.Nbt;

/// <summary>
/// Writes NBT in the Java Edition big-endian format. For Network NBT (protocol &gt;= 764,
/// see docs/data/wire_types.md) the root compound carries no name-length prefix.
/// </summary>
public static class NbtWriter
{
    private static readonly UTF8Encoding _modifiedUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Writes a Network NBT root compound: the Compound type byte (0x0A) followed directly by
    /// the compound payload (children then TAG_End), with no root name length prefix.
    /// </summary>
    public static void WriteRoot(PacketWriter writer, NbtCompound root)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(root);

        writer.WriteByte((byte)NbtTagType.CompoundTag);
        WriteCompoundPayload(writer, root);
    }

    /// <summary>
    /// Writes any NBT node as a Network NBT root: the node's type byte followed directly by its
    /// payload, with no root name length prefix. Used for fields whose root may be a non-compound
    /// (e.g. a Text Component encoded as a String tag; docs/data/wire_types.md).
    /// </summary>
    public static void WriteRootNode(PacketWriter writer, NbtNode node)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(node);

        writer.WriteByte((byte)node.TagType);
        WritePayload(writer, node);
    }

    /// <summary>
    /// Writes a named compound tag (type byte + name + children + TAG_End). Used for compounds
    /// nested inside a parent compound.
    /// </summary>
    private static void WriteNamedCompound(PacketWriter writer, string name, NbtCompound compound)
    {
        writer.WriteByte((byte)NbtTagType.CompoundTag);
        WriteModifiedUtf8(writer, name);
        WriteCompoundPayload(writer, compound);
    }

    private static void WriteCompoundPayload(PacketWriter writer, NbtCompound compound)
    {
        foreach (NbtNamedTag tag in compound.Tags)
        {
            WriteNamedTag(writer, tag);
        }

        writer.WriteByte((byte)NbtTagType.End);
    }

    /// <summary>Writes a named tag (type byte + name + payload) inside a compound.</summary>
    private static void WriteNamedTag(PacketWriter writer, NbtNamedTag tag)
    {
        NbtNode value = tag.Value;
        writer.WriteByte((byte)value.TagType);
        WriteModifiedUtf8(writer, tag.Name);

        WritePayload(writer, value);
    }

    private static void WritePayload(PacketWriter writer, NbtNode node)
    {
        switch (node)
        {
            case NbtByte b:
                writer.WriteByte((byte)b.Value);
                break;
            case NbtShort s:
                writer.WriteShort(s.Value);
                break;
            case NbtInt i:
                writer.WriteInt(i.Value);
                break;
            case NbtLong l:
                writer.WriteLong(l.Value);
                break;
            case NbtFloat f:
                writer.WriteFloat(f.Value);
                break;
            case NbtDouble d:
                writer.WriteDouble(d.Value);
                break;
            case NbtByteArray ba:
                writer.WriteInt(ba.Value.Length);
                foreach (sbyte value in ba.Value)
                {
                    writer.WriteByte((byte)value);
                }

                break;
            case NbtString st:
                WriteModifiedUtf8(writer, st.Value);
                break;
            case NbtList list:
                WriteListPayload(writer, list);
                break;
            case NbtCompound compound:
                WriteCompoundPayload(writer, compound);
                break;
            case NbtIntArray ia:
                writer.WriteInt(ia.Value.Length);
                foreach (int value in ia.Value)
                {
                    writer.WriteInt(value);
                }

                break;
            case NbtLongArray la:
                writer.WriteInt(la.Value.Length);
                foreach (long value in la.Value)
                {
                    writer.WriteLong(value);
                }

                break;
            default:
                throw new InvalidOperationException($"Unsupported NBT node type {node.GetType().Name}.");
        }
    }

    private static void WriteListPayload(PacketWriter writer, NbtList list)
    {
        writer.WriteByte((byte)list.ElementType);
        writer.WriteInt(list.Values.Count);

        foreach (NbtNode element in list.Values)
        {
            // List elements are nameless: only the payload is written.
            WritePayload(writer, element);
        }
    }

    /// <summary>
    /// Writes a modified UTF-8 string: an unsigned short byte-length prefix followed by the bytes.
    /// Modified UTF-8 differs from standard UTF-8 in how supplementary characters are encoded
    /// (as surrogate-pair byte sequences), but ASCII and BMP control/non-surrogate text is identical.
    /// </summary>
    private static void WriteModifiedUtf8(PacketWriter writer, string value)
    {
        int byteCount = _modifiedUtf8.GetByteCount(value);
        if (byteCount > ushort.MaxValue)
        {
            throw new InvalidDataException(
                $"NBT string of {byteCount} bytes exceeds the unsigned-short limit of {ushort.MaxValue}.");
        }

        writer.WriteUnsignedShort((ushort)byteCount);
        var buffer = new byte[byteCount];
        _modifiedUtf8.GetBytes(value, buffer);
        writer.WriteBytes(buffer);
    }
}
