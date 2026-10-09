using System.Buffers;
using System.IO.Pipelines;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server.Configuration;
using VoXelSharp.Server.Status;

namespace VoXelSharp.Server.Connections;

/// <summary>
/// A single client connection session. Drives the read loop for one connection:
/// socket bytes -&gt; frames -&gt; packets -&gt; typed packets (via <see cref="PacketRegistry"/>),
/// then writes responses. Malformed input or protocol violations close the connection
/// (log -&gt; disconnect -&gt; cleanup); they never escape to the accept loop or the process.
/// </summary>
public sealed class ConnectionSession
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
    private readonly ILogger _logger;
    private ConfigurationStage _configurationStage = ConfigurationStage.None;

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
    /// <param name="logger">Logger for this session.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public ConnectionSession(
        IDuplexPipe transport,
        PacketRegistry registry,
        ConnectionStateMachine state,
        IStatusJsonProvider statusJson,
        IConfigurationDataProvider configurationData,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(statusJson);
        ArgumentNullException.ThrowIfNull(configurationData);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _registry = registry;
        _state = state;
        _statusJson = statusJson;
        _configurationData = configurationData;
        _logger = logger;
        _buffer = new byte[1024];
    }

    /// <summary>
    /// Runs the session until the connection closes, is cancelled, or a protocol violation occurs.
    /// </summary>
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
            await _transport.Output.CompleteAsync();
            _transport.Input.Complete();
        }
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
                return HandleAcknowledgeFinishConfiguration();

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
        var profile = new GameProfile(
            loginStart.PlayerUuidMsb,
            loginStart.PlayerUuidLsb,
            loginStart.Name,
            [],
            null);

        // Session ID is not yet meaningful here; echo the player's UUID.
        await SendAsync(
            LoginSuccessCodec.Instance,
            new LoginSuccessPacket(profile, loginStart.PlayerUuidMsb, loginStart.PlayerUuidLsb),
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
    /// play", so the session transitions to Play and stays open.
    /// </summary>
    private bool HandleAcknowledgeFinishConfiguration()
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
        return false;
    }

    private async Task SendAsync(IPacketCodec codec, IPacket packet, CancellationToken cancellationToken)
    {
        var payloadWriter = new PacketWriter();
        codec.Encode(packet, payloadWriter);

        byte[] payload = payloadWriter.ToArray();
        byte[] frame = new byte[FrameLength.MaxEncodedLength + payload.Length];
        int written = FrameCodec.EncodeFrame(payload, frame);

        await _transport.Output.WriteAsync(frame.AsMemory(0, written), cancellationToken);
        await _transport.Output.FlushAsync(cancellationToken);
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
