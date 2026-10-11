namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Game Event (play/clientbound, packet ID 39, official name "game_event"). Sends a named
/// game event with a float parameter. Field order per docs/data/packets.json and the 26.3
/// client (ClientboundGameEventPacket): Event (Unsigned Byte), Value (Float).
/// </summary>
public sealed class GameEventPacket : IPacket
{
    /// <summary>
    /// Event ID 13 (LEVEL_CHUNKS_LOAD_START). Sent once the initial chunk batch has been sent;
    /// the client begins the world-loading phase and leaves the "Loading terrain..." screen once
    /// the chunk around the player has rendered (or after a 30-second timeout).
    /// </summary>
    public const byte LevelChunksLoadStart = 13;

    public GameEventPacket(byte eventId, float value)
    {
        EventId = eventId;
        Value = value;
    }

    /// <summary>The event identifier (see the constants on this class for known values).</summary>
    public byte EventId { get; }

    /// <summary>The event parameter; e.g. 0.0 for <see cref="LevelChunksLoadStart"/>.</summary>
    public float Value { get; }
}

/// <summary>Codec for <see cref="GameEventPacket"/>.</summary>
public sealed class GameEventCodec : PacketCodec<GameEventPacket>
{
    /// <summary>Singleton access.</summary>
    public static GameEventCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 39;

    /// <inheritdoc />
    protected override void EncodeBody(GameEventPacket packet, PacketWriter writer)
    {
        writer.WriteByte(packet.EventId);
        writer.WriteFloat(packet.Value);
    }

    /// <inheritdoc />
    protected override GameEventPacket DecodeBody(ref PacketReader reader)
    {
        byte eventId = reader.ReadByte();
        float value = reader.ReadFloat();
        return new GameEventPacket(eventId, value);
    }
}
