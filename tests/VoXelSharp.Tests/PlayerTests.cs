using VoXelSharp.Protocol;
using VoXelSharp.Server.Play;

namespace VoXelSharp.Tests;

public class PlayerTests
{
    private static Player CreatePlayer() => new(entityId: 1);

    [Fact]
    public void EntityIdMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Player(entityId: 0));
    }

    [Fact]
    public void SetProfileStoresProfile()
    {
        var player = CreatePlayer();
        var profile = new GameProfile(1, 2, "Player1", []);

        player.SetProfile(profile);

        Assert.Same(profile, player.Profile);
    }

    [Fact]
    public void SetPositionSeedsPositionAndRotation()
    {
        var player = CreatePlayer();

        player.SetPosition(0.5, 64.0, -0.5, 180.0f, 0.0f);

        Assert.Equal(0.5, player.X);
        Assert.Equal(64.0, player.FeetY);
        Assert.Equal(-0.5, player.Z);
        Assert.Equal(180.0f, player.Yaw);
        Assert.Equal(0.0f, player.Pitch);
    }

    [Fact]
    public void ApplyPositionUpdatesStateAndTick()
    {
        var player = CreatePlayer();
        player.SetPosition(0, 0, 0, 0, 0);

        player.ApplyPosition(1.0, 65.0, -2.0, onGround: true, horizontalCollision: false, tick: 12);

        Assert.Equal(1.0, player.X);
        Assert.Equal(65.0, player.FeetY);
        Assert.Equal(-2.0, player.Z);
        Assert.True(player.OnGround);
        Assert.False(player.HorizontalCollision);
        Assert.Equal(12, player.LastMoveTick);
    }

    [Fact]
    public void ApplyRotationUpdatesRotationAndFlagsButNotPosition()
    {
        var player = CreatePlayer();
        player.SetPosition(0.5, 64.0, 0.5, 0, 0);

        player.ApplyRotation(90.0f, 45.0f, onGround: false, horizontalCollision: true, tick: 20);

        Assert.Equal(0.5, player.X);
        Assert.Equal(64.0, player.FeetY);
        Assert.Equal(0.5, player.Z);
        Assert.Equal(90.0f, player.Yaw);
        Assert.Equal(45.0f, player.Pitch);
        Assert.False(player.OnGround);
        Assert.True(player.HorizontalCollision);
        Assert.Equal(20, player.LastMoveTick);
    }

    [Fact]
    public void ApplyStatusUpdatesOnlyFlagsAndTick()
    {
        var player = CreatePlayer();
        player.SetPosition(0.5, 64.0, 0.5, 30.0f, -10.0f);

        player.ApplyStatus(onGround: true, horizontalCollision: false, tick: 30);

        Assert.Equal(0.5, player.X);
        Assert.Equal(64.0, player.FeetY);
        Assert.Equal(30.0f, player.Yaw);
        Assert.Equal(-10.0f, player.Pitch);
        Assert.True(player.OnGround);
        Assert.False(player.HorizontalCollision);
        Assert.Equal(30, player.LastMoveTick);
    }
}
