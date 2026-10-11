namespace VoXelSharp.Server;

/// <summary>
/// Allocates monotonically increasing entity IDs for world entities (item drops, etc.).
/// The ID space must not collide with player IDs; players are allocated separately in
/// <see cref="MinecraftServer"/>, so this allocator starts above any player ID range and the
/// server must not re-use player IDs above this allocator's start. Safe for concurrent use.
/// </summary>
public sealed class ServerEntityIds
{
    private readonly int _start;

    public ServerEntityIds(int start = 1)
    {
        _start = start;
    }

    private int _next;

    /// <summary>Returns the next entity ID.</summary>
    public int Next() => Interlocked.Increment(ref _next) + _start - 1;
}
