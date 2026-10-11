using System.Buffers;
using System.IO.Pipelines;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Nbt;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server.Configuration;
using VoXelSharp.Server.Play;
using VoXelSharp.Server.Status;
using VoXelSharp.World;

namespace VoXelSharp.Server.Connections;

/// <summary>
/// A single client connection session. Drives the read loop for one connection:
/// socket bytes -&gt; frames -&gt; packets -&gt; typed packets (via <see cref="PacketRegistry"/>),
/// then writes responses. Malformed input or protocol violations close the connection
/// (log -&gt; disconnect -&gt; cleanup); they never escape to the accept loop or the process.
/// </summary>
public sealed class ConnectionSession : IDisposable
{
    private static readonly Action<ILogger, string, Exception?> _protocolViolation =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, nameof(ConnectionSession)), "Protocol violation, closing connection: {Message}");

    private static readonly Action<ILogger, string, Exception?> _ioFailure =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, nameof(ConnectionSession)), "Connection I/O failure: {Message}");

    private static readonly Action<ILogger, Exception?> _unexpectedError =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, nameof(ConnectionSession)), "Unexpected error while running connection session.");

    private static readonly Action<ILogger, HandshakeIntent, Exception?> _unsupportedFlow =
        LoggerMessage.Define<HandshakeIntent>(LogLevel.Information, new EventId(4, nameof(ConnectionSession)), "Client requested the {Intent} flow, which is not implemented yet; closing connection.");

    /// <summary>The step of the Configuration flow this session has reached.</summary>
    private enum ConfigurationStage
    {
        /// <summary>Not in the Configuration state yet.</summary>
        None,

        /// <summary>Sent Known Packs (clientbound); waiting for the client's Known Packs (serverbound) response.</summary>
        AwaitingKnownPacks,

        /// <summary>Sent Registry Data, Update Tags, Feature Flags, and Finish Configuration; waiting for the acknowledge.</summary>
        AwaitingFinishAck,

        /// <summary>Configuration finished; the session is in Play.</summary>
        Play,
    }

    private readonly IDuplexPipe _transport;
    private readonly PacketRegistry _registry;
    private readonly ConnectionStateMachine _state;
    private readonly IStatusJsonProvider _statusJson;
    private readonly IConfigurationDataProvider _configurationData;
    private readonly IPlayDataProvider _playData;
    private readonly IWorldAccess _world;
    private readonly ILogger _logger;
    private readonly TimeSpan _keepAliveInterval;
    private readonly Func<long> _getTick;
    private long _fallbackTick;
    private ConfigurationStage _configurationStage = ConfigurationStage.None;

    // Play session state, set once the session reaches Play.
    private GameProfile? _playerProfile;
    private Player? _player;
    private int _nextTeleportId = 1;
    private int _lastIssuedTeleportId;
    private int _nextKeepAliveId = 1;
    private CancellationTokenSource? _playCts;
    private Task? _keepAliveTask;

    // Block placement state: the selected hotbar slot (set_carried_item) and the creative-mode
    // item the client has placed in each slot (set_creative_mode_slot).
    private int _carriedSlot;
    private readonly CreativeSlotItem[] _creativeItemBySlot = new CreativeSlotItem[9];

    // Serializes all writes to the transport (the keep-alive loop writes concurrently with
    // the read-loop responses).
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Frame accumulation buffer: incoming bytes are appended until a complete frame is available.
    private byte[] _buffer;
    private int _bufferCount;

    /// <summary>
    /// Creates a session for one connection.
    /// </summary>
    /// <param name="transport">The duplex transport (e.g. framed network stream) for this connection.</param>
    /// <param name="registry">The packet registry; must be fully registered before any decode.</param>
    /// <param name="state">Per-connection protocol state machine.</param>
    /// <param name="statusJson">Provides the status JSON used for Status Response packets.</param>
    /// <param name="configurationData">Provides the data sent during the Configuration state.</param>
    /// <param name="playData">Provides the world/chunk data sent once the session reaches Play.</param>
    /// <param name="world">The shared server world used for chunk data and block changes.</param>
    /// <param name="logger">Logger for this session.</param>
    /// <param name="keepAliveInterval">Interval between Play Keep Alive packets (default 15s).</param>
    /// <param name="getTick">Provides the current server tick for movement timestamps; defaults to a per-session counter when the server tick loop is not wired in.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public ConnectionSession(
        IDuplexPipe transport,
        PacketRegistry registry,
        ConnectionStateMachine state,
        IStatusJsonProvider statusJson,
        IConfigurationDataProvider configurationData,
        IPlayDataProvider playData,
        IWorldAccess world,
        ILogger logger,
        TimeSpan? keepAliveInterval = null,
        Func<long>? getTick = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(statusJson);
        ArgumentNullException.ThrowIfNull(configurationData);
        ArgumentNullException.ThrowIfNull(playData);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _registry = registry;
        _state = state;
        _statusJson = statusJson;
        _configurationData = configurationData;
        _playData = playData;
        _world = world;
        _logger = logger;
        _keepAliveInterval = keepAliveInterval ?? TimeSpan.FromSeconds(15);
        _getTick = getTick ?? (() => Interlocked.Increment(ref _fallbackTick));
        _buffer = new byte[1024];
    }

    /// <summary>The server-side player state for this connection, or <see langword="null"/> before Login Start.</summary>
    public Player? Player => _player;

    /// <summary>Runs the session until the connection closes, is cancelled, or a protocol violation occurs.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReadLoopAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        catch (InvalidDataException exception)
        {
            _protocolViolation(_logger, exception.Message, null);
        }
        catch (IOException exception)
        {
            _ioFailure(_logger, exception.Message, null);
        }
        catch (Exception exception)
        {
            _unexpectedError(_logger, exception);
        }
        finally
        {
            _playCts?.Cancel();
            await ObserveKeepAliveTaskAsync();
            _playCts?.Dispose();
            _writeLock.Dispose();
            await _transport.Output.CompleteAsync();
            _transport.Input.Complete();
        }
    }

    /// <summary>
    /// Waits for background-loop shutdown and observes any unhandled exception so it cannot
    /// escape the session's finally block or become an unobserved task exception. The transport
    /// is about to be completed, so teardown-time failures are expected and are logged, not thrown.
    /// </summary>
    private async Task ObserveKeepAliveTaskAsync()
    {
        if (_keepAliveTask is null)
        {
            return;
        }

        try
        {
            await _keepAliveTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected: the token was cancelled to stop the loop.
        }
        catch (ObjectDisposedException exception)
        {
            _ioFailure(_logger, exception.Message, null);
        }
        catch (IOException exception)
        {
            _ioFailure(_logger, exception.Message, null);
        }
        catch (TimeoutException)
        {
            // The loop did not stop in time; the transport is completed anyway and the task
            // is abandoned at session end (its exceptions are observed by the loop itself).
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _playCts?.Dispose();
        _writeLock.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ReadResult result = await _transport.Input.ReadAsync(cancellationToken);
            if (result.IsCanceled)
            {
                break;
            }

            AppendToBuffer(result.Buffer);
            _transport.Input.AdvanceTo(result.Buffer.End);

            bool close = false;
            while (!close && TryReadFrame(out byte[] payload))
            {
                close = await DispatchAsync(payload, cancellationToken);
            }

            if (close)
            {
                break;
            }

            if (result.IsCompleted)
            {
                // EOF with unprocessed bytes means a truncated frame, which is a protocol violation.
                if (_bufferCount > 0)
                {
                    throw new InvalidDataException("Connection closed with a partial frame pending.");
                }

                break;
            }
        }
    }

    /// <summary>
    /// Decodes and handles one packet payload. Returns true when the connection must close.
    /// </summary>
    private async Task<bool> DispatchAsync(byte[] payload, CancellationToken cancellationToken)
    {
        IPacket packet = _registry.Decode(_state.State, PacketDirection.Serverbound, payload);

        switch (packet)
        {
            case HandshakePacket handshake:
                return HandleHandshake(handshake);

            case StatusRequestPacket:
                await SendAsync(
                    StatusResponseCodec.Instance,
                    new StatusResponsePacket(_statusJson.GetStatusJson()),
                    cancellationToken);
                return false;

            case PingRequestPacket ping:
                await SendAsync(
                    PongResponseCodec.Instance,
                    new PongResponsePacket(ping.Timestamp),
                    cancellationToken);
                // The wiki Server List Ping flow: server sends Pong and then closes the connection.
                return true;

            case LoginStartPacket loginStart:
                await HandleLoginStartAsync(loginStart, cancellationToken);
                return false;

            case LoginAcknowledgedPacket:
                await HandleLoginAcknowledgedAsync(cancellationToken);
                return false;

            case ClientInformationPacket:
                // Accepted; the client's settings are not used by any session logic yet.
                return false;

            case KnownPacksPacket:
                await SendConfigurationDataAsync(cancellationToken);
                return false;

            case AcknowledgeFinishConfigurationPacket:
                await HandleAcknowledgeFinishConfigurationAsync(cancellationToken);
                return false;

            case ConfirmTeleportationPacket confirm:
                return HandleConfirmTeleportation(confirm);

            case SetPlayerPositionAndRotationPacket move:
                return await HandlePlayerMoveAsync(move, cancellationToken);

            case SetPlayerPositionPacket move:
                return await HandlePlayerMoveAsync(move, cancellationToken);

            case SetPlayerRotationPacket move:
                return await HandlePlayerMoveAsync(move, cancellationToken);

            case SetPlayerStatusOnlyPacket move:
                return await HandlePlayerMoveAsync(move, cancellationToken);

            case PunchPacket:
                // Left-click: no combat/block-breaking interaction logic in this milestone.
                return false;

            case PlayerCommandPacket:
                // Sneak/sprint/other player commands; no gameplay effect yet.
                return false;

            case SetCarriedItemPacket carriedItem:
                _carriedSlot = carriedItem.Slot;
                return false;

            case SetCreativeModeSlotPacket creativeSlot:
                _creativeItemBySlot[creativeSlot.Slot & 0xFF] = creativeSlot.Item;
                return false;

            case PlayerActionPacket playerAction:
                return await HandlePlayerActionAsync(playerAction, cancellationToken);

            case UseItemOnPacket useItemOn:
                return await HandleUseItemOnAsync(useItemOn, cancellationToken);

            case UseItemPacket:
                // Right-click using the held item; accepted, no interaction logic yet.
                return false;

            case AttackPacket:
                // Attacking an entity; no combat logic in this milestone.
                return false;

            case ContainerClickPacket:
            case ContainerClosePacket:
                // Inventory/container operations are not modelled yet; accepted.
                return false;

            case MoveVehiclePacket:
                // Riding vehicles; accompanied by player_input while mounted.
                return false;

            case ServerPlayerAbilitiesPacket:
                // Flying toggles; no flight logic in this milestone.
                return false;

            case PlayerInputPacket:
                // Forward/back/strafe/jump/sprint inputs; movement authority is client-side here.
                return false;

            case RecipeBookChangeSettingsPacket:
                // Recipe book UI state; nothing consumes it yet.
                return false;

            case ClientCommandPacket:
                // Client status requests (e.g. respawn); accepted.
                return false;

            case ChunkBatchReceivedPacket:
                // The client reports its desired chunk rate; the flat-world join sequence
                // sends all initial chunks at once, so there is nothing to throttle.
                return false;

            case PlayerLoadedPacket:
                // The client has finished loading; no further action is required yet.
                return false;

            case ClientTickEndPacket:
                // Expected once per client tick; nothing to do in this milestone.
                return false;

            case PlayKeepAlivePacket keepAlive:
                return HandlePlayKeepAlive(keepAlive);

            case PlayPongPacket:
                // Response to a future clientbound Ping; accepted and ignored for now.
                return false;

            case ServerboundChatPacket chat:
                return await HandleChatAsync(chat, cancellationToken);

            case ServerboundChatAckPacket:
                // The client acks messages it has received; nothing tracks ack offsets yet.
                return false;

            case ServerboundChatCommandPacket:
            case ServerboundChatCommandSignedPacket:
                // Client-side commands are not implemented yet; accepted to stay connected.
                return false;

            case ServerboundChatSessionUpdatePacket:
                // The client announces its chat session; the key material is not used in this milestone.
                return false;

            case PluginMessagePacket pluginMessage:
                // Minecraft itself uses plugin channels (e.g. the client announces its brand on
                // minecraft:brand after login). No channel is used by session logic yet, so all
                // serverbound plugin messages are accepted and ignored (never a disconnect).
                return false;

            default:
                throw new InvalidDataException(
                    $"Packet '{packet.GetType().Name}' is not valid in state {_state.State} ({PacketDirection.Serverbound}).");
        }
    }

    private bool HandleHandshake(HandshakePacket handshake)
    {
        if (handshake.Intent == HandshakeIntent.Login)
        {
            // Begin the login flow (Login Start -> Login Success -> Login Acknowledged -> Configuration).
            if (!_state.TryTransitionTo(ConnectionState.Login))
            {
                throw new InvalidDataException(
                    $"Handshake intent {handshake.Intent} is not a valid transition from state {_state.State}.");
            }

            return false;
        }

        if (handshake.Intent == HandshakeIntent.Transfer)
        {
            // The transfer flow is implemented in a later milestone.
            _unsupportedFlow(_logger, handshake.Intent, null);
            return true;
        }

        if (!_state.TryTransitionTo(ConnectionState.Status))
        {
            throw new InvalidDataException(
                $"Handshake intent {handshake.Intent} is not a valid transition from state {_state.State}.");
        }

        return false;
    }

    /// <summary>
    /// Handles a Login Start by sending the client a Login Success. The player's profile
    /// (name and UUID) is echoed from the Login Start packet. No authentication or encryption
    /// is performed in this milestone, so the login is accepted as-is and the session remains
    /// in the Login state awaiting Login Acknowledged.
    /// </summary>
    private async Task HandleLoginStartAsync(LoginStartPacket loginStart, CancellationToken cancellationToken)
    {
        _playerProfile = new GameProfile(
            loginStart.PlayerUuidMsb,
            loginStart.PlayerUuidLsb,
            loginStart.Name,
            []);

        _player = new Player(_playData.PlayerEntityId);
        _player.SetProfile(_playerProfile);

        // Session ID is not yet meaningful here; echo the player's UUID.
        await SendAsync(
            LoginSuccessCodec.Instance,
            new LoginSuccessPacket(_playerProfile, loginStart.PlayerUuidMsb, loginStart.PlayerUuidLsb),
            cancellationToken);
    }

    /// <summary>
    /// Handles Login Acknowledged, the client's confirmation after Login Success, by moving
    /// the session to the Configuration state and sending the Known Packs (clientbound)
    /// packet that begins the known-packs negotiation. The session then waits for the
    /// client's Known Packs (serverbound) response before sending the rest.
    /// </summary>
    private async Task HandleLoginAcknowledgedAsync(CancellationToken cancellationToken)
    {
        if (!_state.TryTransitionTo(ConnectionState.Configuration))
        {
            throw new InvalidDataException(
                $"Login Acknowledged is not a valid transition from state {_state.State}.");
        }

        _configurationStage = ConfigurationStage.AwaitingKnownPacks;

        // Wiki "Known packs negotiation": the server first announces its data packs
        // (minecraft:core is the only pack a vanilla client recognizes) and the vanilla
        // server does not continue with Configuration until the client responds.
        await SendAsync(
            KnownPacksCodec.Instance,
            _configurationData.KnownPacks,
            cancellationToken);
    }

    /// <summary>
    /// Sends the remainder of the mandatory Configuration sequence once the client has
    /// responded with its serverbound Known Packs: Registry Data (one packet per registry),
    /// Update Tags, Feature Flags, and then Finish Configuration. The wiki flow is
    /// Registry Data -&gt; Update Tags -&gt; Finish Configuration; Feature Flags are sent
    /// before Finish Configuration so the client enables features before leaving Configuration.
    /// </summary>
    private async Task SendConfigurationDataAsync(CancellationToken cancellationToken)
    {
        if (_configurationStage != ConfigurationStage.AwaitingKnownPacks)
        {
            throw new InvalidDataException(
                $"Unexpected Known Packs (serverbound) in Configuration stage {_configurationStage}.");
        }

        foreach (RegistryDataPacket registry in _configurationData.RegistryData)
        {
            await SendAsync(RegistryDataCodec.Instance, registry, cancellationToken);
        }

        await SendAsync(UpdateTagsCodec.Instance, _configurationData.UpdateTags, cancellationToken);
        await SendAsync(FeatureFlagsCodec.Instance, _configurationData.FeatureFlags, cancellationToken);
        await SendAsync(FinishConfigurationCodec.Instance, new FinishConfigurationPacket(), cancellationToken);

        _configurationStage = ConfigurationStage.AwaitingFinishAck;
    }

    /// <summary>
    /// Handles Acknowledge Finish Configuration, the client's confirmation after receiving
    /// Finish Configuration. The wiki states this packet "switches the connection state to
    /// play". The session transitions to Play and then sends the initial join sequence:
    /// Login (play), Set Center Chunk, Set Default Spawn Position, Set Simulation Distance,
    /// Set Held Item, Chunk Batch Start, Chunk Data for the view area, Chunk Batch Finished,
    /// Set Render Distance, Synchronize Player Position, a Player Info Add Player entry, and
    /// then periodic Keep Alive while the connection stays open.
    /// </summary>
    private async Task HandleAcknowledgeFinishConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_configurationStage != ConfigurationStage.AwaitingFinishAck)
        {
            throw new InvalidDataException(
                $"Unexpected Acknowledge Finish Configuration in Configuration stage {_configurationStage}.");
        }

        if (!_state.TryTransitionTo(ConnectionState.Play))
        {
            throw new InvalidDataException(
                $"Acknowledge Finish Configuration is not a valid transition from state {_state.State}.");
        }

        _configurationStage = ConfigurationStage.Play;

        // Login (play) is always the first packet in the Play state.
        await SendAsync(LoginPlayCodec.Instance, BuildLoginPlay(), cancellationToken);

        // The Notchian server announces its brand right after Login (play); the client
        // tolerates its absence but sends its own brand back via a serverbound plugin message.
        await SendAsync(
            PlayClientboundPluginMessageCodec.Instance,
            BuildBrandMessage(),
            cancellationToken);

        // Player Info Add Player so the client shows the player in the tab list.
        await SendPlayerInfoAddAsync(cancellationToken);

        BlockPosition spawn = _playData.SpawnBlock;
        int centerChunkX = spawn.X >> 4;
        int centerChunkZ = spawn.Z >> 4;

        await SendAsync(
            SetCenterChunkCodec.Instance,
            new SetCenterChunkPacket(centerChunkX, centerChunkZ),
            cancellationToken);

        await SendAsync(
            SetDefaultSpawnPositionCodec.Instance,
            new SetDefaultSpawnPositionPacket(_playData.DimensionName, spawn, 0f, 0f),
            cancellationToken);

        await SendAsync(
            SetSimulationDistanceCodec.Instance,
            new SetSimulationDistancePacket(_playData.SimulationDistance),
            cancellationToken);

        await SendAsync(SetHeldItemCodec.Instance, new SetHeldItemPacket(0), cancellationToken);

        await SendAsync(ChunkBatchStartCodec.Instance, new ChunkBatchStartPacket(), cancellationToken);

        int radius = _playData.ViewDistance;
        int sentChunks = 0;
        for (int chunkZ = centerChunkZ - radius; chunkZ <= centerChunkZ + radius; chunkZ++)
        {
            for (int chunkX = centerChunkX - radius; chunkX <= centerChunkX + radius; chunkX++)
            {
                await SendChunkDataAsync(chunkX, chunkZ, cancellationToken);
                sentChunks++;
            }
        }

        await SendAsync(
            ChunkBatchFinishedCodec.Instance,
            new ChunkBatchFinishedPacket(sentChunks),
            cancellationToken);

        // LEVEL_CHUNKS_LOAD_START: the initial chunk batch is fully sent; the client now
        // transitions into world loading and leaves the "Loading terrain..." screen when the
        // chunk around the player has rendered (26.3 ClientboundGameEventPacket event 13).
        await SendAsync(
            GameEventCodec.Instance,
            new GameEventPacket(GameEventPacket.LevelChunksLoadStart, 0.0f),
            cancellationToken);

        await SendAsync(
            SetRenderDistanceCodec.Instance,
            new SetRenderDistancePacket(_playData.ViewDistance),
            cancellationToken);

        int teleportId = _nextTeleportId++;
        _lastIssuedTeleportId = teleportId;
        await SendAsync(
            SynchronizePlayerPositionCodec.Instance,
            new SynchronizePlayerPositionPacket(
                teleportId,
                _playData.SpawnX,
                _playData.SpawnY,
                _playData.SpawnZ,
                0.0,
                0.0,
                0.0,
                0f,
                0f,
                TeleportFlags.None),
            cancellationToken);

        // Seed the authoritative server-side player position at the spawn point; from now on
        // move_player packets update this state (feet Y, per the 26.3 move packet).
        _player?.SetPosition(_playData.SpawnX, _playData.SpawnY, _playData.SpawnZ, 0f, 0f);

        // Start the periodic keep-alive loop for the lifetime of this Play session.
        _playCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(_playCts.Token), _playCts.Token);
    }

    private static PluginMessagePacket BuildBrandMessage()
    {
        // The minecraft:brand channel Data is a String (32767) carrying the implementation name.
        var brandWriter = new PacketWriter();
        brandWriter.WriteString("VoXelSharp", PluginMessagePacket.MaxChannelLength);
        return new PluginMessagePacket(PluginMessagePacket.BrandChannel, brandWriter.ToArray());
    }

    private LoginPlayPacket BuildLoginPlay()
    {
        return new LoginPlayPacket(
            _playData.PlayerEntityId,
            isHardcore: false,
            _playData.DimensionNames,
            ServerPlayDataProvider.MaxPlayers,
            _playData.ViewDistance,
            _playData.SimulationDistance,
            reducedDebugInfo: false,
            enableRespawnScreen: true,
            doLimitedCrafting: false,
            _playData.DimensionTypeId,
            _playData.DimensionName,
            hashedSeed: 0,
            PlayerGameMode.Survival,
            OptionalPlayerGameMode.Undefined,
            isDebug: false,
            isFlat: true,
            hasDeathLocation: false,
            deathDimensionName: null,
            deathLocation: null,
            portalCooldown: 0,
            seaLevel: 63,
            onlineMode: false,
            enforcesSecureChat: false);
    }

    private async Task SendPlayerInfoAddAsync(CancellationToken cancellationToken)
    {
        // The profile is always set during Login Start before the session can reach Play; a
        // missing profile here means the session reached Play out of order.
        if (_playerProfile is null)
        {
            throw new InvalidDataException("Cannot send Player Info Add without a player profile.");
        }

        GameProfile profile = _playerProfile;
        var entry = new PlayerInfoEntryData(
            profile.PlayerUuidMsb,
            profile.PlayerUuidLsb,
            new PlayerInfoAddPlayerData(profile.Username, profile.Properties),
            ChatSession: null,
            GameMode: (int)PlayerGameMode.Survival,
            Listed: true,
            Ping: 0,
            DisplayName: null,
            ListPriority: 0,
            HatVisible: false);

        await SendAsync(
            PlayerInfoUpdateCodec.Instance,
            new PlayerInfoUpdatePacket(PlayerInfoActions.AddPlayer, [entry]),
            cancellationToken);
    }

    private async Task SendChunkDataAsync(int chunkX, int chunkZ, CancellationToken cancellationToken)
    {
        FlatWorldChunk worldChunk = _world.GetChunk(chunkX, chunkZ);
        byte[] data = ChunkDataIO.EncodeSections(worldChunk.Sections);

        var packet = new ChunkDataPacket(
            chunkX,
            chunkZ,
            worldChunk.Heightmaps,
            data,
            [],
            worldChunk.Light);

        await SendAsync(ChunkDataCodec.Instance, packet, cancellationToken);
    }

    /// <summary>
    /// Periodically sends a Keep Alive so the client does not time out. The echo check in
    /// <see cref="HandlePlayKeepAlive"/> validates that the client responds with the expected ID.
    /// </summary>
    private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_keepAliveInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                int keepAliveId = _nextKeepAliveId++;
                await SendAsync(
                    PlayKeepAliveCodec.Instance,
                    new PlayKeepAlivePacket(keepAliveId),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Session shutdown.
        }
        catch (IOException exception)
        {
            // The peer closed while we were sending; the read loop will notice and tear down.
            _ioFailure(_logger, exception.Message, null);
        }
        catch (ObjectDisposedException exception)
        {
            // The write lock was disposed during teardown while a send was in flight.
            _ioFailure(_logger, exception.Message, null);
        }
    }

    private bool HandleConfirmTeleportation(ConfirmTeleportationPacket confirm)
    {
        // The client echoes the Teleport ID from Synchronize Player Position. Any previous
        // movement packets before the matching confirmation are ignored (wiki Synchronize
        // Player Position notes), so a mismatched confirmation is a protocol violation.
        if (_configurationStage != ConfigurationStage.Play)
        {
            throw new InvalidDataException("Confirm Teleportation received before the session reached Play.");
        }

        if (confirm.TeleportId != _lastIssuedTeleportId)
        {
            throw new InvalidDataException(
                $"Confirm Teleportation ID {confirm.TeleportId} does not match the last issued teleport {_lastIssuedTeleportId}.");
        }

        return false;
    }

    private async Task<bool> HandlePlayerMoveAsync(SetPlayerPositionAndRotationPacket move, CancellationToken cancellationToken)
    {
        EnsurePlayerMoveIsInPlay();
        ValidatePosition(move.X, move.FeetY, move.Z);
        long tick = _getTick();
        if (_player is not null && !PlayerMovementValidator.IsMoveAllowed(_player, move.X, move.FeetY, move.Z, tick))
        {
            await CorrectPlayerPositionAsync(cancellationToken);
            return false;
        }

        _player?.ApplyPosition(
            move.X, move.FeetY, move.Z,
            onGround: (move.Flags & 0x01) != 0,
            horizontalCollision: (move.Flags & 0x02) != 0,
            tick);
        return false;
    }

    private async Task<bool> HandlePlayerMoveAsync(SetPlayerPositionPacket move, CancellationToken cancellationToken)
    {
        EnsurePlayerMoveIsInPlay();
        ValidatePosition(move.X, move.FeetY, move.Z);
        long tick = _getTick();
        if (_player is not null && !PlayerMovementValidator.IsMoveAllowed(_player, move.X, move.FeetY, move.Z, tick))
        {
            await CorrectPlayerPositionAsync(cancellationToken);
            return false;
        }

        _player?.ApplyPosition(
            move.X, move.FeetY, move.Z,
            onGround: (move.Flags & 0x01) != 0,
            horizontalCollision: (move.Flags & 0x02) != 0,
            tick);
        return false;
    }

    private Task<bool> HandlePlayerMoveAsync(SetPlayerRotationPacket move, CancellationToken cancellationToken)
    {
        EnsurePlayerMoveIsInPlay();
        _player?.ApplyRotation(
            move.Yaw, move.Pitch,
            onGround: (move.Flags & 0x01) != 0,
            horizontalCollision: (move.Flags & 0x02) != 0,
            tick: _getTick());
        return Task.FromResult(false);
    }

    private Task<bool> HandlePlayerMoveAsync(SetPlayerStatusOnlyPacket move, CancellationToken cancellationToken)
    {
        EnsurePlayerMoveIsInPlay();
        _player?.ApplyStatus(
            onGround: (move.Flags & 0x01) != 0,
            horizontalCollision: (move.Flags & 0x02) != 0,
            tick: _getTick());
        return Task.FromResult(false);
    }

    /// <summary>
    /// Teleports the client back to the player's last accepted position after an impossible
    /// movement (gameplay validation: reject/correct the action while keeping the connection
    /// alive, per AGENTS.md "Error Handling"). If no position has been accepted yet, the spawn
    /// point is used.
    /// </summary>
    private async Task CorrectPlayerPositionAsync(CancellationToken cancellationToken)
    {
        if (_player is null)
        {
            return;
        }

        int teleportId = _nextTeleportId++;
        _lastIssuedTeleportId = teleportId;
        await SendAsync(
            SynchronizePlayerPositionCodec.Instance,
            new SynchronizePlayerPositionPacket(
                teleportId,
                _player.X,
                _player.FeetY,
                _player.Z,
                0.0,
                0.0,
                0.0,
                _player.Yaw,
                _player.Pitch,
                TeleportFlags.None),
            cancellationToken);
    }

    private void EnsurePlayerMoveIsInPlay()
    {
        if (_configurationStage != ConfigurationStage.Play)
        {
            throw new InvalidDataException("Player movement received before the session reached Play.");
        }
    }

    private static void ValidatePosition(double x, double y, double z)
    {
        // Reject non-finite coordinates (docs/PROTOCOL.md "Position and Coordinates").
        if (double.IsNaN(x) || double.IsInfinity(x)
            || double.IsNaN(y) || double.IsInfinity(y)
            || double.IsNaN(z) || double.IsInfinity(z))
        {
            throw new InvalidDataException("Player position contains a non-finite value.");
        }
    }

    private async Task<bool> HandlePlayerActionAsync(PlayerActionPacket action, CancellationToken cancellationToken)
    {
        EnsurePlayerMoveIsInPlay();

        // The block is broken when the client reports it finished digging
        // (player_action STOP_DESTROY_BLOCK). Requires the interaction distance to be valid.
        if (action.Status == PlayerActionPacket.StopDestroyBlock
            && _player is not null
            && PlayerMovementValidator.IsWithinInteractionRange(_player, action.Location.X, action.Location.Y, action.Location.Z))
        {
            int blockStateId = _world.GetBlock(action.Location.X, action.Location.Y, action.Location.Z);
            if (blockStateId != BlockStateIds.Air)
            {
                _world.SetBlock(action.Location.X, action.Location.Y, action.Location.Z, BlockStateIds.Air);
                await SendAsync(
                    BlockUpdateCodec.Instance,
                    new BlockUpdatePacket(action.Location, BlockStateIds.Air),
                    cancellationToken);
            }
        }

        // Acknowledge the action's sequence so the client stops its block-break animation
        // (26.3 ClientboundBlockChangedAckPacket, docs/data/packets.json block_changed_ack).
        await SendAsync(
            BlockChangedAckCodec.Instance,
            new BlockChangedAckPacket(action.Sequence),
            cancellationToken);
        return false;
    }

    /// <summary>
    /// Handles creative block placement: the client right-clicked a surface (use_item_on) with
    /// a block from its hotbar. The block is placed in the cell adjacent to the clicked face
    /// (Location + face normal, 26.3 Direction data values), validated for interaction range and
    /// world bounds, then broadcast via block_update and acknowledged with block_changed_ack.
    /// One-hotbar selection (set_carried_item) + creative slots (set_creative_mode_slot) decide
    /// what is held; unknown/empty items are ignored (connection stays alive).
    /// </summary>
    private async Task<bool> HandleUseItemOnAsync(UseItemOnPacket useItemOn, CancellationToken cancellationToken)
    {
        EnsurePlayerMoveIsInPlay();

        var target = TargetCellFor(useItemOn);
        int blockStateId = CreativeBlockCatalog.BlockStateIdForItem(HeldItemId());
        if (blockStateId >= 0
            && _player is not null
            && PlayerMovementValidator.IsWithinInteractionRange(_player, target.X, target.Y, target.Z)
            && _world.GetBlock(target.X, target.Y, target.Z) == BlockStateIds.Air)
        {
            _world.SetBlock(target.X, target.Y, target.Z, blockStateId);
            await SendAsync(
                BlockUpdateCodec.Instance,
                new BlockUpdatePacket(target, blockStateId),
                cancellationToken);
        }

        // Acknowledge the placement sequence (block_changed_ack).
        await SendAsync(
            BlockChangedAckCodec.Instance,
            new BlockChangedAckPacket(useItemOn.Sequence),
            cancellationToken);
        return false;
    }

    /// <summary>The item registry ID held in the selected hotbar slot, or -1 when empty.</summary>
    private int HeldItemId()
    {
        CreativeSlotItem? held = _creativeItemBySlot[_carriedSlot & 0xFF];
        return held is not null && !held.IsEmpty ? held.ItemId : -1;
    }

    /// <summary>
    /// The cell the placed block goes in: the clicked block's location offset by the clicked
    /// face's normal (Direction data value 0=down, 1=up, 2=north, 3=south, 4=west, 5=east).
    /// Verified from the 26.3 client <c>Direction</c> enum.
    /// </summary>
    private static BlockPosition TargetCellFor(UseItemOnPacket useItemOn)
    {
        int face = useItemOn.Face;
        int dx = 0, dy = 0, dz = 0;
        switch (face)
        {
            case 0: dy = -1; break; // down
            case 1: dy = 1; break;  // up
            case 2: dz = -1; break; // north
            case 3: dz = 1; break;  // south
            case 4: dx = -1; break; // west
            case 5: dx = 1; break;  // east
            default: break;
        }

        return new BlockPosition(useItemOn.Location.X + dx, useItemOn.Location.Y + dy, useItemOn.Location.Z + dz);
    }

    /// <summary>
    /// Echoes a chat message back to the player as a System Chat (clientbound, id 124) using a
    /// plain-text NBT component, so the player "sees chat" (ROADMAP M5). In this milestone there is
    /// a single player, so the message is returned to the sender; a later milestone broadcasts to
    /// all sessions. The signed-chat signature is deliberately not validated.
    /// </summary>
    private async Task<bool> HandleChatAsync(ServerboundChatPacket chat, CancellationToken cancellationToken)
    {
        EnsurePlayerMoveIsInPlay();

        string author = _player?.Profile?.Username ?? "Player";
        await SendAsync(
            SystemChatCodec.Instance,
            new SystemChatPacket(new NbtString($"{author}: {chat.Message}"), overlay: false),
            cancellationToken);
        return false;
    }

    private bool HandlePlayKeepAlive(PlayKeepAlivePacket keepAlive)
    {
        // The client echoes one of the Keep Alive IDs the server has issued. Any unknown ID
        // means the client is out of sync; treat it as a protocol violation (vanilla only
        // recognizes IDs from the pending keep-alive queue).
        if (keepAlive.KeepAliveId < 1 || keepAlive.KeepAliveId >= _nextKeepAliveId)
        {
            throw new InvalidDataException(
                $"Keep Alive ID {keepAlive.KeepAliveId} was never issued by this session.");
        }

        return false;
    }

    private async Task SendAsync(IPacketCodec codec, IPacket packet, CancellationToken cancellationToken)
    {
        var payloadWriter = new PacketWriter();
        codec.Encode(packet, payloadWriter);

        byte[] payload = payloadWriter.ToArray();
        byte[] frame = new byte[FrameLength.MaxEncodedLength + payload.Length];
        int written = FrameCodec.EncodeFrame(payload, frame);

        // Serialize writes: the keep-alive loop sends concurrently with read-loop responses.
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _transport.Output.WriteAsync(frame.AsMemory(0, written), cancellationToken);
            await _transport.Output.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void AppendToBuffer(ReadOnlySequence<byte> data)
    {
        int required = _bufferCount + (int)data.Length;
        if (required > _buffer.Length)
        {
            int newSize = Math.Max(required, _buffer.Length * 2);
            Array.Resize(ref _buffer, newSize);
        }

        foreach (ReadOnlyMemory<byte> segment in data)
        {
            segment.Span.CopyTo(_buffer.AsSpan(_bufferCount));
            _bufferCount += segment.Length;
        }
    }

    /// <summary>
    /// Extracts one complete frame payload from the buffer; compacts the buffer when consumed.
    /// </summary>
    private bool TryReadFrame(out byte[] payload)
    {
        payload = [];

        if (!FrameCodec.TryReadFrame(_buffer.AsSpan(0, _bufferCount), out byte[] decoded, out int consumed))
        {
            return false;
        }

        payload = decoded;
        int remaining = _bufferCount - consumed;
        if (remaining > 0)
        {
            Buffer.BlockCopy(_buffer, consumed, _buffer, 0, remaining);
        }

        _bufferCount = remaining;
        return true;
    }
}
