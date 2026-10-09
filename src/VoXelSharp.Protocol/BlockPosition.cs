namespace VoXelSharp.Protocol;

/// <summary>
/// A network block position (docs/data/wire_types.md, wiki "Data types" 2026-10-09).
/// Encoded as one big-endian 64-bit integer: x as a 26-bit signed int in the 38 high bits,
/// z as a 26-bit signed int, y as a 12-bit signed int in the low bits.
/// Valid ranges: x, z in [-33,554,432, 33,554,431]; y in [-2,048, 2,047].
/// </summary>
public readonly record struct BlockPosition(int X, int Y, int Z)
{
    /// <summary>Minimum x and z (26-bit signed range lower bound, -2^25).</summary>
    public const int MinHorizontalCoordinate = -33_554_432;

    /// <summary>Maximum x and z (26-bit signed range upper bound, 2^25 - 1).</summary>
    public const int MaxHorizontalCoordinate = 33_554_431;

    /// <summary>Minimum y (12-bit signed range lower bound, -2^11).</summary>
    public const int MinVerticalCoordinate = -2_048;

    /// <summary>Maximum y (12-bit signed range upper bound, 2^11 - 1).</summary>
    public const int MaxVerticalCoordinate = 2_047;

    /// <summary>Encodes the position into the 64-bit wire integer (docs/data/wire_types.md).</summary>
    public long ToPacked()
        => (((long)X & 0x3FFFFFF) << 38)
         | (((long)Z & 0x3FFFFFF) << 12)
         | ((long)Y & 0xFFF);

    /// <summary>Decodes a 64-bit wire integer into a position (docs/data/wire_types.md).</summary>
    public static BlockPosition FromPacked(long value)
        => new(
            (int)(value >> 38),
            (int)(value << 52 >> 52),
            (int)(value << 26 >> 38));
}
