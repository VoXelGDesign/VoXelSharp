namespace VoXelSharp.Protocol.Nbt;

/// <summary>
/// NBT tag type IDs, per the Minecraft Wiki "NBT format" spec (docs/data/wire_types.md).
/// These IDs are the type discriminator bytes used on the wire.
/// </summary>
public enum NbtTagType : byte
{
    /// <summary>Terminates a compound; also used as the type of an empty list.</summary>
    End = 0,

    /// <summary>A single signed byte.</summary>
    ByteTag = 1,

    /// <summary>A signed big-endian 16-bit integer.</summary>
    ShortTag = 2,

    /// <summary>A signed big-endian 32-bit integer.</summary>
    IntTag = 3,

    /// <summary>A signed big-endian 64-bit integer.</summary>
    LongTag = 4,

    /// <summary>A big-endian IEEE-754 single-precision float.</summary>
    FloatTag = 5,

    /// <summary>A big-endian IEEE-754 double-precision float.</summary>
    DoubleTag = 6,

    /// <summary>A length-prefixed array of signed bytes.</summary>
    ByteArrayTag = 7,

    /// <summary>A length-prefixed (unsigned short) modified UTF-8 string.</summary>
    StringTag = 8,

    /// <summary>A list of nameless tags, all of the same type.</summary>
    ListTag = 9,

    /// <summary>A compound of named tags.</summary>
    CompoundTag = 10,

    /// <summary>A length-prefixed array of signed ints.</summary>
    IntArrayTag = 11,

    /// <summary>A length-prefixed array of signed longs.</summary>
    LongArrayTag = 12,
}
