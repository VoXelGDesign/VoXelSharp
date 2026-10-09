namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Teleport Flags bit mask for Synchronize Player Position (docs/data/wire_types.md).
/// A set bit means the corresponding value is relative to the player's current state.
/// </summary>
[Flags]
#pragma warning disable CA1711 // "Flags" is the protocol field name (docs/data/wire_types.md "Teleport Flags").
public enum TeleportFlags
{
    None = 0x000,
    RelativeX = 0x001,
    RelativeY = 0x002,
    RelativeZ = 0x004,
    RelativeYaw = 0x008,
    RelativePitch = 0x010,
    RelativeVelocityX = 0x020,
    RelativeVelocityY = 0x040,
    RelativeVelocityZ = 0x080,
    RotateVelocityWithRotation = 0x100,
}

/// <summary>
/// Synchronize Player Position (play/clientbound, packet ID 73, official name
/// "player_position"). Teleports the player; the client must confirm with a serverbound
/// Confirm Teleportation containing the same Teleport ID.
/// Field order per docs/data/packets.json: Teleport ID (VarInt), X/Y/Z (Double),
/// Velocity X/Y/Z (Double), Yaw/Pitch (Float), Flags (Teleport Flags = Int bit mask).
/// Verified as protocol_id 73 against the Mojang data-generator report.
/// </summary>
public sealed class SynchronizePlayerPositionPacket : IPacket
{
    public SynchronizePlayerPositionPacket(
        int teleportId,
        double x,
        double y,
        double z,
        double velocityX,
        double velocityY,
        double velocityZ,
        float yaw,
        float pitch,
        TeleportFlags flags)
    {
        TeleportId = teleportId;
        X = x;
        Y = y;
        Z = z;
        VelocityX = velocityX;
        VelocityY = velocityY;
        VelocityZ = velocityZ;
        Yaw = yaw;
        Pitch = pitch;
        Flags = flags;
    }

    /// <summary>Echoed back by the client in Confirm Teleportation.</summary>
    public int TeleportId { get; }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public double VelocityX { get; }

    public double VelocityY { get; }

    public double VelocityZ { get; }

    public float Yaw { get; }

    public float Pitch { get; }

    public TeleportFlags Flags { get; }
}

/// <summary>Codec for <see cref="SynchronizePlayerPositionPacket"/>.</summary>
public sealed class SynchronizePlayerPositionCodec : PacketCodec<SynchronizePlayerPositionPacket>
{
    /// <summary>Singleton access.</summary>
    public static SynchronizePlayerPositionCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 73;

    /// <inheritdoc />
    protected override void EncodeBody(SynchronizePlayerPositionPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.TeleportId);
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.Y);
        writer.WriteDouble(packet.Z);
        writer.WriteDouble(packet.VelocityX);
        writer.WriteDouble(packet.VelocityY);
        writer.WriteDouble(packet.VelocityZ);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
        writer.WriteInt((int)packet.Flags);
    }

    /// <inheritdoc />
    protected override SynchronizePlayerPositionPacket DecodeBody(ref PacketReader reader)
    {
        int teleportId = reader.ReadVarInt();
        double x = reader.ReadDouble();
        double y = reader.ReadDouble();
        double z = reader.ReadDouble();
        double velocityX = reader.ReadDouble();
        double velocityY = reader.ReadDouble();
        double velocityZ = reader.ReadDouble();
        float yaw = reader.ReadFloat();
        float pitch = reader.ReadFloat();
        int flags = reader.ReadInt();

        return new SynchronizePlayerPositionPacket(
            teleportId, x, y, z, velocityX, velocityY, velocityZ, yaw, pitch, (TeleportFlags)flags);
    }
}
