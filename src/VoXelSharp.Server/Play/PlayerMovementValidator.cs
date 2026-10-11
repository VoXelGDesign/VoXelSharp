namespace VoXelSharp.Server.Play;

/// <summary>
/// Server-side gameplay validation for <c>move_player_*</c> packets (ROADMAP M5 "Security").
///
/// A move is rejected (and the player corrected by teleport-back) when it is clearly
/// impossible: outside the world's vertical bounds, past the horizontal world border,
/// farther than one packet could legitimately travel, or faster than the maximum
/// plausible per-tick horizontal speed.
///
/// Limits are server-side tuning (gameplay validation, not protocol facts):
/// - Vertical bounds come from the flat-world dimension (minY = -64, height = 384,
///   per docs/ROADMAP.md M4). Feet Y must stay within [minY, minY + height + 1] so a
///   jump on the top block of the build ceiling does not false-positive.
/// - The horizontal bound is the vanilla default world border (29,999,984 each axis).
/// - A single move packet covering more than 100 blocks is rejected immediately (the
///   vanilla "moved too quickly" threshold).
/// - Horizontal speed is capped at 5 blocks per tick; the vanilla client never exceeds
///   ~0.3 blocks/tick on foot (creative flying is slower than 1.1 blocks/tick).
/// </summary>
public static class PlayerMovementValidator
{
    /// <summary>Minimum buildable Y for the vanilla overworld (flat-world dimension).</summary>
    public const double WorldMinY = -64;

    /// <summary>World height in blocks (flat-world dimension).</summary>
    public const double WorldHeight = 384;

    /// <summary>
    /// Maximum feet Y, inclusive: the top buildable block is at <see cref="WorldMinY"/> +
    /// <see cref="WorldHeight"/> - 1; allow one extra block so standing/jumping on the
    /// ceiling is not flagged.
    /// </summary>
    public const double MaxFeetY = WorldMinY + WorldHeight + 1;

    /// <summary>Absolute horizontal world-border coordinate (vanilla default border 29,999,984).</summary>
    public const double WorldBorder = 29_999_984;

    /// <summary>Maximum distance covered by a single move packet, in blocks (vanilla "moved too quickly" threshold).</summary>
    public const double MaxSingleMoveDistance = 100;

    /// <summary>Maximum plausible horizontal speed in blocks per tick (generous anti-cheat cap).</summary>
    public const double MaxHorizontalSpeedPerTick = 5;

    /// <summary>
    /// Maximum player-to-block-centre distance for block breaking/placing, in blocks.
    /// Matches the vanilla server's MAX_INTERACTION_DISTANCE (docs/ROADMAP.md M5 Security:
    /// "invalid interaction distances").
    /// </summary>
    public const double MaxInteractionDistance = 6;

    /// <summary>
    /// Returns whether a move to (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>)
    /// is plausible given the player's last applied position and the tick it was applied on.
    /// </summary>
    public static bool IsMoveAllowed(Player player, double x, double y, double z, long currentTick)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (!IsWithinWorldBounds(x, y, z))
        {
            return false;
        }

        double dx = x - player.X;
        double dy = y - player.FeetY;
        double dz = z - player.Z;
        if (Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)) > MaxSingleMoveDistance)
        {
            return false;
        }

        // No speed check before the first real move: LastMoveTick is 0 after the join
        // sequence seeds the spawn position, and the client's very first move may differ
        // from the teleport target by normal rounding.
        if (player.LastMoveTick > 0)
        {
            long elapsedTicks = Math.Max(1, currentTick - player.LastMoveTick);
            double horizontalDistance = Math.Sqrt((dx * dx) + (dz * dz));
            if (horizontalDistance > MaxHorizontalSpeedPerTick * elapsedTicks)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the position is inside the overworld's vertical range and the world border.</summary>
    public static bool IsWithinWorldBounds(double x, double y, double z)
        => y >= WorldMinY
           && y <= MaxFeetY
           && Math.Abs(x) <= WorldBorder
           && Math.Abs(z) <= WorldBorder;

    /// <summary>
    /// Returns whether the player is within <see cref="MaxInteractionDistance"/> (squared) of the
    /// given block position. The player's feet position is allowed to be up to one block away from
    /// the target block face, so the check compares the player's feet against the block centre.
    /// </summary>
    public static bool IsWithinInteractionRange(Player player, int blockX, int blockY, int blockZ)
    {
        ArgumentNullException.ThrowIfNull(player);

        double dx = player.X - (blockX + 0.5);
        double dy = player.FeetY - (blockY + 0.5);
        double dz = player.Z - (blockZ + 0.5);
        return (dx * dx) + (dy * dy) + (dz * dz) <= MaxInteractionDistance * MaxInteractionDistance;
    }
}
