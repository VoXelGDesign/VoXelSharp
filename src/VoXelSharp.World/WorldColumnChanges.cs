namespace VoXelSharp.World;

/// <summary>
/// A single changed block in the persisted world: world coordinates plus the global block
/// state ID to write there. Stored as world coordinates (not internal flat indices) so the
/// save format is independent of the chunk column's in-memory layout.
/// </summary>
public sealed record WorldBlockChange(int X, int Y, int Z, int GlobalBlockStateId);

/// <summary>
/// The changed blocks of one chunk column, relative to the <see cref="FlatWorldGenerator"/>
/// seed terrain. Only columns with at least one change are written to the save file; columns
/// that still match the generated terrain are omitted and regenerate for free on load.
/// </summary>
public sealed record WorldColumnChanges(int ChunkX, int ChunkZ, WorldBlockChange[] Changes);
