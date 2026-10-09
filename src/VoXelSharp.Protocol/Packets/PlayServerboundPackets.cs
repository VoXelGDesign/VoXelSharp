namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Confirm Teleportation (play/serverbound, packet ID 0, official name "accept_teleportation").
/// The client confirms a Synchronize Player Position by echoing its Teleport ID and reporting
/// the resulting position/rotation.
/// Field order per docs/data/packets.json: Teleport ID (VarInt), X/Y/Z (Double), Yaw/Pitch (Float).
/// </summary>
public sealed class ConfirmTeleportationPacket : IPacket
{
    public ConfirmTeleportationPacket(int teleportId, double x, double y, double z, float yaw, float pitch)
    {
        TeleportId = teleportId;
        X = x;
        Y = y;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
    }

    public int TeleportId { get; }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public float Yaw { get; }

    public float Pitch { get; }
}

/// <summary>Codec for <see cref="ConfirmTeleportationPacket"/>.</summary>
public sealed class ConfirmTeleportationCodec : PacketCodec<ConfirmTeleportationPacket>
{
    /// <summary>Singleton access.</summary>
    public static ConfirmTeleportationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(ConfirmTeleportationPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.TeleportId);
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.Y);
        writer.WriteDouble(packet.Z);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
    }

    /// <inheritdoc />
    protected override ConfirmTeleportationPacket DecodeBody(ref PacketReader reader)
        => new(
            reader.ReadVarInt(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadFloat(),
            reader.ReadFloat());
}

/// <summary>
/// Set Player Position and Rotation (play/serverbound, packet ID 31, official name
/// "move_player_pos_rot"). Field order per docs/data/packets.json: X/Y/Z (Double, feet Y),
/// Yaw/Pitch (Float), Flags (Byte bit field: 0x01 on ground, 0x02 pushing against wall).
/// </summary>
public sealed class SetPlayerPositionAndRotationPacket : IPacket
{
    public SetPlayerPositionAndRotationPacket(double x, double feetY, double z, float yaw, float pitch, byte flags)
    {
        X = x;
        FeetY = feetY;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
        Flags = flags;
    }

    public double X { get; }

    /// <summary>Absolute feet position (normally Head Y - 1.62).</summary>
    public double FeetY { get; }

    public double Z { get; }

    public float Yaw { get; }

    public float Pitch { get; }

    /// <summary>0x01 on ground, 0x02 pushing against wall.</summary>
    public byte Flags { get; }
}

/// <summary>Codec for <see cref="SetPlayerPositionAndRotationPacket"/>.</summary>
public sealed class SetPlayerPositionAndRotationCodec : PacketCodec<SetPlayerPositionAndRotationPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetPlayerPositionAndRotationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 31;

    /// <inheritdoc />
    protected override void EncodeBody(SetPlayerPositionAndRotationPacket packet, PacketWriter writer)
    {
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.FeetY);
        writer.WriteDouble(packet.Z);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
        writer.WriteByte(packet.Flags);
    }

    /// <inheritdoc />
    protected override SetPlayerPositionAndRotationPacket DecodeBody(ref PacketReader reader)
        => new(
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadFloat(),
            reader.ReadFloat(),
            reader.ReadByte());
}

/// <summary>
/// Chunk Batch Received (play/serverbound, packet ID 11, official name "chunk_batch_received").
/// The client sends this after receiving a Chunk Batch Started/Finished pair.
/// Field: Chunks per tick (Float).
/// </summary>
public sealed class ChunkBatchReceivedPacket : IPacket
{
    public ChunkBatchReceivedPacket(float chunksPerTick)
    {
        ChunksPerTick = chunksPerTick;
    }

    /// <summary>Desired chunks per tick.</summary>
    public float ChunksPerTick { get; }
}

/// <summary>Codec for <see cref="ChunkBatchReceivedPacket"/>.</summary>
public sealed class ChunkBatchReceivedCodec : PacketCodec<ChunkBatchReceivedPacket>
{
    /// <summary>Singleton access.</summary>
    public static ChunkBatchReceivedCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 11;

    /// <inheritdoc />
    protected override void EncodeBody(ChunkBatchReceivedPacket packet, PacketWriter writer)
        => writer.WriteFloat(packet.ChunksPerTick);

    /// <inheritdoc />
    protected override ChunkBatchReceivedPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadFloat());
}

/// <summary>
/// Player Loaded (play/serverbound, packet ID 44, official name "player_loaded").
/// Sent once the client considers itself fully loaded (first chunk batch received).
/// No fields.
/// </summary>
public sealed class PlayerLoadedPacket : IPacket
{
}

/// <summary>Codec for <see cref="PlayerLoadedPacket"/>.</summary>
public sealed class PlayerLoadedCodec : PacketCodec<PlayerLoadedPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayerLoadedCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 44;

    /// <inheritdoc />
    protected override void EncodeBody(PlayerLoadedPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override PlayerLoadedPacket DecodeBody(ref PacketReader reader) => new();
}

/// <summary>
/// Client Tick End (play/serverbound, packet ID 13, official name "client_tick_end").
/// Sent at the end of every client tick. No fields.
/// </summary>
public sealed class ClientTickEndPacket : IPacket
{
}

/// <summary>Codec for <see cref="ClientTickEndPacket"/>.</summary>
public sealed class ClientTickEndCodec : PacketCodec<ClientTickEndPacket>
{
    /// <summary>Singleton access.</summary>
    public static ClientTickEndCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 13;

    /// <inheritdoc />
    protected override void EncodeBody(ClientTickEndPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override ClientTickEndPacket DecodeBody(ref PacketReader reader) => new();
}

/// <summary>
/// Pong (play/serverbound, packet ID 45, official name "pong").
/// Client response to the clientbound Ping packet. Field: ID (Int).
/// </summary>
public sealed class PlayPongPacket : IPacket
{
    public PlayPongPacket(int id)
    {
        Id = id;
    }

    public int Id { get; }
}

/// <summary>Codec for <see cref="PlayPongPacket"/>.</summary>
public sealed class PlayPongCodec : PacketCodec<PlayPongPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayPongCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 45;

    /// <inheritdoc />
    protected override void EncodeBody(PlayPongPacket packet, PacketWriter writer)
        => writer.WriteInt(packet.Id);

    /// <inheritdoc />
    protected override PlayPongPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadInt());
}
