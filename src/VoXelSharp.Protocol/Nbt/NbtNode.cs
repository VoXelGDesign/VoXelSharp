namespace VoXelSharp.Protocol.Nbt;

/// <summary>
/// A parsed NBT tag value. Network NBT (docs/data/wire_types.md) carries an unnamed root
/// <see cref="NbtCompound"/>; fields use the concrete tag kinds below.
/// </summary>
public abstract class NbtNode
{
    /// <summary>The tag type this node represents.</summary>
    public abstract NbtTagType TagType { get; }

    private protected NbtNode()
    {
    }
}

/// <summary>A signed byte tag.</summary>
public sealed class NbtByte : NbtNode
{
    public NbtByte(sbyte value) => Value = value;

    public override NbtTagType TagType => NbtTagType.ByteTag;

    public sbyte Value { get; }
}

/// <summary>A signed short tag.</summary>
public sealed class NbtShort : NbtNode
{
    public NbtShort(short value) => Value = value;

    public override NbtTagType TagType => NbtTagType.ShortTag;

    public short Value { get; }
}

/// <summary>A signed int tag.</summary>
public sealed class NbtInt : NbtNode
{
    public NbtInt(int value) => Value = value;

    public override NbtTagType TagType => NbtTagType.IntTag;

    public int Value { get; }
}

/// <summary>A signed long tag.</summary>
public sealed class NbtLong : NbtNode
{
    public NbtLong(long value) => Value = value;

    public override NbtTagType TagType => NbtTagType.LongTag;

    public long Value { get; }
}

/// <summary>A single-precision float tag.</summary>
public sealed class NbtFloat : NbtNode
{
    public NbtFloat(float value) => Value = value;

    public override NbtTagType TagType => NbtTagType.FloatTag;

    public float Value { get; }
}

/// <summary>A double-precision float tag.</summary>
public sealed class NbtDouble : NbtNode
{
    public NbtDouble(double value) => Value = value;

    public override NbtTagType TagType => NbtTagType.DoubleTag;

    public double Value { get; }
}

/// <summary>A byte-array tag.</summary>
public sealed class NbtByteArray : NbtNode
{
    public NbtByteArray(sbyte[] value) => Value = value ?? throw new ArgumentNullException(nameof(value));

    public override NbtTagType TagType => NbtTagType.ByteArrayTag;

    public sbyte[] Value { get; }
}

/// <summary>A modified UTF-8 string tag.</summary>
public sealed class NbtString : NbtNode
{
    public NbtString(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));

    public override NbtTagType TagType => NbtTagType.StringTag;

    public string Value { get; }
}

/// <summary>A list of nameless tags, all of the same type.</summary>
public sealed class NbtList : NbtNode
{
    public NbtList(NbtTagType elementType, List<NbtNode> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (NbtNode value in values)
        {
            if (value.TagType != elementType)
            {
                throw new ArgumentException(
                    $"List element type {value.TagType} does not match declared list element type {elementType}.",
                    nameof(values));
            }
        }

        ElementType = elementType;
        Values = values;
    }

    public override NbtTagType TagType => NbtTagType.ListTag;

    /// <summary>The type shared by all elements (End when the list is empty).</summary>
    public NbtTagType ElementType { get; }

    public List<NbtNode> Values { get; }
}

/// <summary>A compound of named tags in order.</summary>
public sealed class NbtCompound : NbtNode
{
    private readonly List<NbtNamedTag> _tags;

    public NbtCompound(IEnumerable<NbtNamedTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags = (tags as List<NbtNamedTag> ?? tags.ToList());
    }

    public override NbtTagType TagType => NbtTagType.CompoundTag;

    public IReadOnlyList<NbtNamedTag> Tags => _tags;
}

/// <summary>A single (name, value) pair inside a <see cref="NbtCompound"/>.</summary>
public sealed record NbtNamedTag(string Name, NbtNode Value);

/// <summary>An int-array tag.</summary>
public sealed class NbtIntArray : NbtNode
{
    public NbtIntArray(int[] value) => Value = value ?? throw new ArgumentNullException(nameof(value));

    public override NbtTagType TagType => NbtTagType.IntArrayTag;

    public int[] Value { get; }
}

/// <summary>A long-array tag.</summary>
public sealed class NbtLongArray : NbtNode
{
    public NbtLongArray(long[] value) => Value = value ?? throw new ArgumentNullException(nameof(value));

    public override NbtTagType TagType => NbtTagType.LongArrayTag;

    public long[] Value { get; }
}
