using VoXelSharp.Protocol;

namespace VoXelSharp.Server.Play;

/// <summary>
/// Server-side representation of a connected player's gameplay state: identity, position,
/// rotation, and movement flags. This is the object M5 gameplay (movement validation,
/// block interaction, broadcast) and later plugin events operate on.
///
/// Movement wire facts (26.3 client <c>ServerboundMovePlayerPacket</c>): move_player packets
/// always carry absolute coordinates, and the trailing flags byte is a bit field —
/// <c>0x01</c> on ground, <c>0x02</c> pushing against a wall. There are no relative-position
/// bits (each packet variant simply omits the fields it does not carry).
/// </summary>
public sealed class Player
{
    /// <summary>
    /// Creates a player with a known entity ID. The profile is set once Login Start is
    /// received (<see cref="SetProfile"/>); position/rotation are set by the join sequence.
    /// </summary>
    public Player(int entityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entityId);
        EntityId = entityId;
    }

    /// <summary>Entity ID assigned by the server (Login (play) packet).</summary>
    public int EntityId { get; }

    /// <summary>The player's profile (name/UUID), set during Login Start.</summary>
    public GameProfile? Profile { get; private set; }

    /// <summary>Absolute feet X (world coordinate).</summary>
    public double X { get; private set; }

    /// <summary>Absolute feet Y (world coordinate; the move packets use feet Y, normally head Y - 1.62).</summary>
    public double FeetY { get; private set; }

    /// <summary>Absolute feet Z (world coordinate).</summary>
    public double Z { get; private set; }

    /// <summary>Horizontal rotation (yaw) in degrees.</summary>
    public float Yaw { get; private set; }

    /// <summary>Vertical rotation (pitch) in degrees.</summary>
    public float Pitch { get; private set; }

    /// <summary>Whether the player is standing on a solid surface (flags bit 0x01).</summary>
    public bool OnGround { get; private set; }

    /// <summary>Whether the player is pushing against a wall (flags bit 0x02).</summary>
    public bool HorizontalCollision { get; private set; }

    /// <summary>The server tick on which the player state was last updated.</summary>
    public long LastMoveTick { get; private set; }

    /// <summary>Attaches the player's profile once Login Start is processed.</summary>
    public void SetProfile(GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
    }

    /// <summary>Places the player at a position (used to seed the spawn position after the join sequence).</summary>
    public void SetPosition(double x, double feetY, double z, float yaw, float pitch)
    {
        X = x;
        FeetY = feetY;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
    }

    /// <summary>Applies a move_player_pos / move_player_pos_rot update (absolute position ± flags).</summary>
    public void ApplyPosition(double x, double feetY, double z, bool onGround, bool horizontalCollision, long tick)
    {
        X = x;
        FeetY = feetY;
        Z = z;
        OnGround = onGround;
        HorizontalCollision = horizontalCollision;
        LastMoveTick = tick;
    }

    /// <summary>Applies a move_player_rot update (rotation + flags only; position is unchanged).</summary>
    public void ApplyRotation(float yaw, float pitch, bool onGround, bool horizontalCollision, long tick)
    {
        Yaw = yaw;
        Pitch = pitch;
        OnGround = onGround;
        HorizontalCollision = horizontalCollision;
        LastMoveTick = tick;
    }

    /// <summary>Applies a move_player_status_only update (flags only; position/rotation unchanged).</summary>
    public void ApplyStatus(bool onGround, bool horizontalCollision, long tick)
    {
        OnGround = onGround;
        HorizontalCollision = horizontalCollision;
        LastMoveTick = tick;
    }
}
