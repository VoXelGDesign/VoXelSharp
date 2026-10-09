namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Set Default Spawn Position (play/clientbound, packet ID 99, official name
/// "set_default_spawn_position"). Sets the world spawn used after respawning.
/// Field order per docs/data/packets.json: Dimension Name (Identifier), Location (Position),
/// Yaw (Float), Pitch (Float). Verified as protocol_id 99 against the Mojang report.
/// </summary>
public sealed class SetDefaultSpawnPositionPacket : IPacket
{
    /// <summary>Maximum Identifier length (protocol String limit).</summary>
    public const int MaxIdentifierLength = 32767;

    public SetDefaultSpawnPositionPacket(string dimensionName, BlockPosition location, float yaw, float pitch)
    {
        ArgumentNullException.ThrowIfNull(dimensionName);
        DimensionName = dimensionName;
        Location = location;
        Yaw = yaw;
        Pitch = pitch;
    }

    public string DimensionName { get; }

    public BlockPosition Location { get; }

    public float Yaw { get; }

    public float Pitch { get; }
}

/// <summary>Codec for <see cref="SetDefaultSpawnPositionPacket"/>.</summary>
public sealed class SetDefaultSpawnPositionCodec : PacketCodec<SetDefaultSpawnPositionPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetDefaultSpawnPositionCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 99;

    /// <inheritdoc />
    protected override void EncodeBody(SetDefaultSpawnPositionPacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.DimensionName, SetDefaultSpawnPositionPacket.MaxIdentifierLength);
        writer.WritePosition(packet.Location);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
    }

    /// <inheritdoc />
    protected override SetDefaultSpawnPositionPacket DecodeBody(ref PacketReader reader)
    {
        string dimensionName = reader.ReadString(SetDefaultSpawnPositionPacket.MaxIdentifierLength);
        BlockPosition location = reader.ReadPosition();
        float yaw = reader.ReadFloat();
        float pitch = reader.ReadFloat();
        return new SetDefaultSpawnPositionPacket(dimensionName, location, yaw, pitch);
    }
}

/// <summary>
/// Set Simulation Distance (play/clientbound, packet ID 113, official name
/// "set_simulation_distance"). Field: Simulation Distance (VarInt).
/// </summary>
public sealed class SetSimulationDistancePacket : IPacket
{
    public SetSimulationDistancePacket(int simulationDistance)
    {
        SimulationDistance = simulationDistance;
    }

    public int SimulationDistance { get; }
}

/// <summary>Codec for <see cref="SetSimulationDistancePacket"/>.</summary>
public sealed class SetSimulationDistanceCodec : PacketCodec<SetSimulationDistancePacket>
{
    /// <summary>Singleton access.</summary>
    public static SetSimulationDistanceCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 113;

    /// <inheritdoc />
    protected override void EncodeBody(SetSimulationDistancePacket packet, PacketWriter writer)
        => writer.WriteVarInt(packet.SimulationDistance);

    /// <inheritdoc />
    protected override SetSimulationDistancePacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt());
}

/// <summary>
/// Set Held Item (play/clientbound, packet ID 107, official name "set_held_slot").
/// Updates the slot number the client highlights in the hotbar (0-8).
/// Field: Slot (VarInt).
/// </summary>
public sealed class SetHeldItemPacket : IPacket
{
    public SetHeldItemPacket(int slot)
    {
        Slot = slot;
    }

    /// <summary>The selected slot (0-8).</summary>
    public int Slot { get; }
}

/// <summary>Codec for <see cref="SetHeldItemPacket"/>.</summary>
public sealed class SetHeldItemCodec : PacketCodec<SetHeldItemPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetHeldItemCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 107;

    /// <inheritdoc />
    protected override void EncodeBody(SetHeldItemPacket packet, PacketWriter writer)
    {
        if (packet.Slot < 0 || packet.Slot > 8)
        {
            throw new InvalidDataException($"Held item slot {packet.Slot} is outside the protocol range 0-8.");
        }

        writer.WriteVarInt(packet.Slot);
    }

    /// <inheritdoc />
    protected override SetHeldItemPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt());
}
