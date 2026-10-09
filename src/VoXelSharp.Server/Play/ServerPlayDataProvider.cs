using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.World;

namespace VoXelSharp.Server.Play;

/// <summary>
/// The default <see cref="IPlayDataProvider"/> for Minecraft 26.3 / protocol 777.
/// Spawns the player over a flat world at chunk (0, 0) with its feet on top of the
/// surface grass block (surface Y = <see cref="FlatWorldGenerator.SurfaceY"/>, so feet
/// Y = surface + 1). The dimension is <c>minecraft:overworld</c> (dimension_type id 0,
/// matching <see cref="Configuration.ServerConfigurationDataProvider"/> registry order).
/// </summary>
public sealed class ServerPlayDataProvider : IPlayDataProvider
{
    /// <summary>The dimension name in the <c>minecraft:dimension_type</c> registry.</summary>
    public const string OverworldDimensionName = "minecraft:overworld";

    /// <summary>Maximum players advertised by the server (docs/PROTOCOL.md).</summary>
    public const int MaxPlayers = 20;

    private static readonly string[] _dimensionNames = [OverworldDimensionName];

    private readonly int _entityId;

    public ServerPlayDataProvider(int playerEntityId)
    {
        _entityId = playerEntityId;
    }

    /// <inheritdoc />
    public int PlayerEntityId => _entityId;

    /// <inheritdoc />
    public int ViewDistance { get; } = 4;

    /// <inheritdoc />
    public int SimulationDistance { get; } = 4;

    /// <inheritdoc />
    public string[] DimensionNames => _dimensionNames;

    /// <inheritdoc />
    public string DimensionName => OverworldDimensionName;

    /// <inheritdoc />
    public int DimensionTypeId => 0;

    /// <inheritdoc />
    public BlockPosition SpawnBlock => new(0, FlatWorldGenerator.SurfaceY + 1, 0);

    /// <inheritdoc />
    public double SpawnX => 0.5;

    /// <inheritdoc />
    public double SpawnY => FlatWorldGenerator.SurfaceY + 1;

    /// <inheritdoc />
    public double SpawnZ => 0.5;

    /// <inheritdoc />
    public FlatWorldChunk GenerateChunk(int chunkX, int chunkZ) => FlatWorldGenerator.Generate(chunkX, chunkZ);
}
