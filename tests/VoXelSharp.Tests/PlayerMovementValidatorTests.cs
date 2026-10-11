using VoXelSharp.Server.Play;

namespace VoXelSharp.Tests;

public class PlayerMovementValidatorTests
{
    private static Player CreatePlayerAt(double x, double y, double z, long lastMoveTick = 0)
    {
        var player = new Player(entityId: 1);
        player.SetPosition(x, y, z, 0f, 0f);
        if (lastMoveTick > 0)
        {
            player.ApplyPosition(x, y, z, onGround: true, horizontalCollision: false, lastMoveTick);
        }

        return player;
    }

    // ---- World bounds ----

    [Fact]
    public void PositionInsideWorldBoundsIsAllowed()
    {
        var player = CreatePlayerAt(0, 0, 0);
        Assert.True(PlayerMovementValidator.IsWithinWorldBounds(10.5, 64.0, -20.5));
        Assert.True(PlayerMovementValidator.IsMoveAllowed(player, 10.5, 64.0, -20.5, currentTick: 1));
    }

    [Fact]
    public void PositionBelowWorldMinimumIsRejected()
    {
        var player = CreatePlayerAt(0, 0, 0);
        Assert.False(PlayerMovementValidator.IsWithinWorldBounds(0, PlayerMovementValidator.WorldMinY - 0.1, 0));
        Assert.False(PlayerMovementValidator.IsMoveAllowed(player, 0, PlayerMovementValidator.WorldMinY - 0.1, 0, 1));
    }

    [Fact]
    public void PositionAboveBuildCeilingIsRejected()
    {
        var player = CreatePlayerAt(0, 0, 0);
        Assert.False(PlayerMovementValidator.IsWithinWorldBounds(0, PlayerMovementValidator.MaxFeetY + 1, 0));
        Assert.False(PlayerMovementValidator.IsMoveAllowed(player, 0, PlayerMovementValidator.MaxFeetY + 1, 0, 1));
    }

    [Fact]
    public void PositionPastWorldBorderIsRejected()
    {
        var player = CreatePlayerAt(0, 0, 0);
        Assert.False(PlayerMovementValidator.IsWithinWorldBounds(PlayerMovementValidator.WorldBorder + 1, 0, 0));
        Assert.False(PlayerMovementValidator.IsMoveAllowed(player, PlayerMovementValidator.WorldBorder + 1, 0, 0, 1));
    }

    // ---- Single-packet distance ----

    [Fact]
    public void SingleMoveBeyondMaxDistanceIsRejected()
    {
        var player = CreatePlayerAt(0, 0, 0, lastMoveTick: 10);
        // 200 blocks in one packet, even though a few ticks elapsed, exceeds the single-packet cap.
        bool allowed = PlayerMovementValidator.IsMoveAllowed(player, 200, 0, 0, currentTick: 30);
        Assert.False(allowed);
    }

    // ---- Speed check ----

    [Fact]
    public void FastHorizontalMoveIsRejected()
    {
        // At 6 blocks horizontal per tick and no prior tick spacing, speed exceeds the 5-block cap.
        var player = CreatePlayerAt(0, 0, 0, lastMoveTick: 10);
        bool allowed = PlayerMovementValidator.IsMoveAllowed(player, 6.0, 0, 0, currentTick: 11);
        Assert.False(allowed);
    }

    [Fact]
    public void LegitimateWalkSpeedIsAllowed()
    {
        var player = CreatePlayerAt(0, 0, 0, lastMoveTick: 10);
        // ~0.28 blocks/tick is a sprinting player; well under the cap.
        bool allowed = PlayerMovementValidator.IsMoveAllowed(player, 0.28, 0, 0, currentTick: 11);
        Assert.True(allowed);
    }

    [Fact]
    public void SpeedIsScaledByElapsedTicks()
    {
        var player = CreatePlayerAt(0, 0, 0, lastMoveTick: 10);
        // 10 blocks across 5 ticks (2/tick) is plausible; the same 10 blocks in 1 tick is not.
        Assert.True(PlayerMovementValidator.IsMoveAllowed(player, 10, 0, 0, currentTick: 15));
        Assert.False(PlayerMovementValidator.IsMoveAllowed(player, 10, 0, 0, currentTick: 11));
    }

    [Fact]
    public void FirstMoveAfterSpawnSkipsSpeedCheck()
    {
        // LastMoveTick is 0 after the join sequence seeds the spawn position; the first move
        // may differ from the teleport target by rounding, so only bounds/distance apply.
        var player = CreatePlayerAt(0, 0, 0);
        Assert.True(PlayerMovementValidator.IsMoveAllowed(player, 3.0, 64.0, 4.0, currentTick: 1));
    }
}
