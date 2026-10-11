namespace VoXelSharp.World;

/// <summary>
/// Read/write access to the shared server world for gameplay logic (block breaking/placement,
/// broadcasting). Implementations keep the authoritative block state; chunk packets are built
/// from this state on demand. The socket layer consumes this interface and never mutates the
/// world directly.
/// </summary>
public interface IWorldAccess
{
    /// <summary>Returns the global block state ID at <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>.</summary>
    int GetBlock(int x, int y, int z);

    /// <summary>
    /// Sets the global block state ID at the given position. Positions outside the world
    /// (vertical <see cref="FlatWorldGenerator.MinY"/>..<see cref="FlatWorldGenerator.MinY"/>
    /// + <see cref="FlatWorldGenerator.WorldHeight"/>) are ignored.
    /// </summary>
    void SetBlock(int x, int y, int z, int globalStateId);

    /// <summary>Builds the chunk column (sections, heightmaps, light) for the given chunk coordinates.</summary>
    FlatWorldChunk GetChunk(int chunkX, int chunkZ);
}
