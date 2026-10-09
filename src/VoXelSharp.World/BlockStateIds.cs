namespace VoXelSharp.World;

/// <summary>
/// Global block state IDs for a Minecraft 26.3 / protocol 777 overworld flat map.
/// These are the numeric IDs sent in paletted containers and are version-specific;
/// values come from <c>docs/data/blocks.json</c> (authoritative, generated from the
/// vanilla 26.3 data generators).
/// </summary>
public static class BlockStateIds
{
    /// <summary><c>minecraft:air</c> — the default empty block.</summary>
    public const int Air = 0;

    /// <summary><c>minecraft:stone</c>.</summary>
    public const int Stone = 1;

    /// <summary><c>minecraft:grass_block</c> with <c>snowy=false</c> (the default surface state).</summary>
    public const int GrassBlock = 9;

    /// <summary><c>minecraft:dirt</c>.</summary>
    public const int Dirt = 10;

    /// <summary><c>minecraft:bedrock</c>.</summary>
    public const int Bedrock = 88;
}
