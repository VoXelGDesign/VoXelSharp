namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Registers packet codecs and resolves them by (state, direction, packet ID).
/// Packet IDs are scoped to state and direction; there is no global packet-ID namespace
/// (docs/PROTOCOL.md Packet Identity).
/// Thread-safety: all <see cref="Register"/> calls must happen during setup, before the registry
/// is used for concurrent decoding; the codec dictionary is read-only afterwards.
/// </summary>
public sealed class PacketRegistry
{
    private readonly Dictionary<(ConnectionState, PacketDirection, int), IPacketCodec> _codecs = [];

    /// <summary>
    /// Creates a registry with the currently implemented packets registered:
    /// Handshaking (Handshake) and Status (Request/Response, Ping/Pong).
    /// </summary>
    public static PacketRegistry CreateM2()
    {
        var registry = new PacketRegistry();
        registry.Register(HandshakeCodec.Instance);
        registry.Register(StatusRequestCodec.Instance);
        registry.Register(StatusResponseCodec.Instance);
        registry.Register(PingRequestCodec.Instance);
        registry.Register(PongResponseCodec.Instance);
        return registry;
    }

    /// <summary>
    /// Registers a codec. Registering a second codec for the same (state, direction, packet ID)
    /// is a bug and throws.
    /// </summary>
    public void Register(IPacketCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);

        var key = (codec.State, codec.Direction, codec.PacketId);
        if (!_codecs.TryAdd(key, codec))
        {
            throw new InvalidOperationException(
                $"A codec for ({key.Item1}, {key.Item2}, packet ID {key.Item3}) is already registered.");
        }
    }

    /// <summary>
    /// Attempts to resolve the codec for a packet identity.
    /// </summary>
    public bool TryGet(ConnectionState state, PacketDirection direction, int packetId, out IPacketCodec? codec)
        => _codecs.TryGetValue((state, direction, packetId), out codec);

    /// <summary>
    /// Resolves the codec for a packet identity.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// Thrown when no codec is registered for the packet identity (unknown packet ID).
    /// </exception>
    public IPacketCodec Get(ConnectionState state, PacketDirection direction, int packetId)
        => TryGet(state, direction, packetId, out IPacketCodec? codec)
            ? codec!
            : throw new InvalidDataException(
                $"Unknown packet ID {packetId} for ({state}, {direction}).");

    /// <summary>
    /// Decodes a complete payload (packet ID VarInt followed by the body) into a typed packet.
    /// The payload must begin with the packet ID, whose identity is validated against the registry.
    /// </summary>
    /// <param name="state">The current connection state.</param>
    /// <param name="direction">The expected packet direction.</param>
    /// <param name="payload">The full packet payload.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the payload has no packet ID, the packet ID is unknown or mismatched with the
    /// state/direction, or the body is malformed or truncated.
    /// </exception>
    public IPacket Decode(ConnectionState state, PacketDirection direction, ReadOnlySpan<byte> payload)
    {
        var reader = new PacketReader(payload);
        return Decode(state, direction, ref reader);
    }

    /// <summary>
    /// Decodes a complete payload into a typed packet, advancing <paramref name="reader"/> past it.
    /// </summary>
    public IPacket Decode(ConnectionState state, PacketDirection direction, ref PacketReader reader)
    {
        if (!reader.TryReadVarInt(out int packetId))
        {
            throw new InvalidDataException("Packet payload is missing its packet ID.");
        }

        IPacketCodec codec = Get(state, direction, packetId);
        IPacket packet = codec.Decode(ref reader);

        if (!reader.IsEmpty)
        {
            throw new InvalidDataException(
                $"{reader.Remaining} trailing byte(s) after packet {packetId} in state {state} ({direction}).");
        }

        return packet;
    }
}
