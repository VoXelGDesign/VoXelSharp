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
    /// Handshaking (Handshake), Status (Request/Response, Ping/Pong), Login
    /// (Start, Success, Acknowledged, Disconnect), and Configuration
    /// (Client Information, Known Packs both directions, Registry Data, Update Tags,
    /// Feature Flags, Finish Configuration, Acknowledge Finish Configuration).
    /// </summary>
    public static PacketRegistry CreateDefault()
    {
        var registry = new PacketRegistry();
        registry.Register(HandshakeCodec.Instance);
        registry.Register(StatusRequestCodec.Instance);
        registry.Register(StatusResponseCodec.Instance);
        registry.Register(PingRequestCodec.Instance);
        registry.Register(PongResponseCodec.Instance);
        registry.Register(LoginAcknowledgedCodec.Instance);
        registry.Register(DisconnectLoginCodec.Instance);
        registry.Register(LoginStartCodec.Instance);
        registry.Register(LoginSuccessCodec.Instance);
        registry.Register(ClientInformationCodec.Instance);
        registry.Register(KnownPacksCodec.Instance);
        registry.Register(ServerKnownPacksCodec.Instance);
        registry.Register(RegistryDataCodec.Instance);
        registry.Register(UpdateTagsCodec.Instance);
        registry.Register(FeatureFlagsCodec.Instance);
        registry.Register(FinishConfigurationCodec.Instance);
        registry.Register(AcknowledgeFinishConfigurationCodec.Instance);
        registry.Register(SetCenterChunkCodec.Instance);
        registry.Register(SetRenderDistanceCodec.Instance);
        registry.Register(ChunkBatchStartCodec.Instance);
        registry.Register(ChunkBatchFinishedCodec.Instance);
        registry.Register(PlayKeepAliveCodec.Instance);
        registry.Register(ServerPlayKeepAliveCodec.Instance);
        registry.Register(LoginPlayCodec.Instance);
        registry.Register(ChunkDataCodec.Instance);
        registry.Register(SynchronizePlayerPositionCodec.Instance);
        registry.Register(SetDefaultSpawnPositionCodec.Instance);
        registry.Register(SetSimulationDistanceCodec.Instance);
        registry.Register(SetHeldItemCodec.Instance);
        registry.Register(PlayClientInformationCodec.Instance);
        registry.Register(ConfirmTeleportationCodec.Instance);
        registry.Register(SetPlayerPositionAndRotationCodec.Instance);
        registry.Register(SetPlayerPositionCodec.Instance);
        registry.Register(SetPlayerRotationCodec.Instance);
        registry.Register(SetPlayerStatusOnlyCodec.Instance);
        registry.Register(PunchCodec.Instance);
        registry.Register(PlayerCommandCodec.Instance);
        registry.Register(SetCarriedItemCodec.Instance);
        registry.Register(PlayerActionCodec.Instance);
        registry.Register(UseItemOnCodec.Instance);
        registry.Register(UseItemCodec.Instance);
        registry.Register(AttackCodec.Instance);
        registry.Register(ContainerClickCodec.Instance);
        registry.Register(ContainerCloseCodec.Instance);
        registry.Register(MoveVehicleCodec.Instance);
        registry.Register(ServerPlayerAbilitiesCodec.Instance);
        registry.Register(PlayerInputCodec.Instance);
        registry.Register(RecipeBookChangeSettingsCodec.Instance);
        registry.Register(ClientCommandCodec.Instance);
        registry.Register(ChunkBatchReceivedCodec.Instance);
        registry.Register(PlayerLoadedCodec.Instance);
        registry.Register(ClientTickEndCodec.Instance);
        registry.Register(PlayPongCodec.Instance);
        registry.Register(SystemChatCodec.Instance);
        registry.Register(PlayerInfoUpdateCodec.Instance);
        registry.Register(PlayerInfoRemoveCodec.Instance);
        registry.Register(ServerConfigurationPluginMessageCodec.Instance);
        registry.Register(PlayServerboundPluginMessageCodec.Instance);
        registry.Register(PlayClientboundPluginMessageCodec.Instance);
        registry.Register(GameEventCodec.Instance);
        registry.Register(BlockUpdateCodec.Instance);
        registry.Register(BlockChangedAckCodec.Instance);
        registry.Register(SetCreativeModeSlotCodec.Instance);
        registry.Register(ServerboundChatCodec.Instance);
        registry.Register(ServerboundChatAckCodec.Instance);
        registry.Register(ServerboundChatCommandCodec.Instance);
        registry.Register(ServerboundChatCommandSignedCodec.Instance);
        registry.Register(ServerboundChatSessionUpdateCodec.Instance);
        registry.Register(PickItemFromBlockCodec.Instance);
        registry.Register(AddEntityCodec.Instance);
        registry.Register(SetEntityDataCodec.Instance);
        registry.Register(TakeItemEntityCodec.Instance);
        registry.Register(SetPlayerInventoryCodec.Instance);
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
