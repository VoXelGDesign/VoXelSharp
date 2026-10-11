using VoXelSharp.Protocol;
using VoXelSharp.World;

namespace VoXelSharp.Server.Play;

/// <summary>
/// Supplies the per-connection data needed to start a player in a world once the session
/// reaches the Play state (Login (play), spawn, view settings, and chunk generation).
/// Mirrors <see cref="Configuration.IConfigurationDataProvider"/> for the Configuration state.
/// </summary>
public interface IPlayDataProvider
{
    /// <summary>The entity ID used for this player in Login (play).</summary>
    int PlayerEntityId { get; }

    /// <summary>Render distance in chunks (protocol range 2-32).</summary>
    int ViewDistance { get; }

    /// <summary>Simulation distance in chunks.</summary>
    int SimulationDistance { get; }

    /// <summary>All dimension identifiers this server exposes.</summary>
    string[] DimensionNames { get; }

    /// <summary>The dimension the player spawns into.</summary>
    string DimensionName { get; }

    /// <summary>ID of the dimension type in the <c>minecraft:dimension_type</c> registry.</summary>
    int DimensionTypeId { get; }

    /// <summary>World spawn as a block position (Set Default Spawn Position).</summary>
    BlockPosition SpawnBlock { get; }

    /// <summary>Spawn feet X for Synchronize Player Position.</summary>
    double SpawnX { get; }

    /// <summary>Spawn feet Y for Synchronize Player Position.</summary>
    double SpawnY { get; }

    /// <summary>Spawn feet Z for Synchronize Player Position.</summary>
    double SpawnZ { get; }
}
