using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server.Configuration;
using VoXelSharp.Server.Connections;
using VoXelSharp.Server.Play;
using VoXelSharp.Server.Status;

namespace VoXelSharp.Server;

/// <summary>
/// Manages the TCP listener, accept loop, and session lifecycle.
/// This is the server entry point for the M2 status/ping protocol.
/// </summary>
public sealed class MinecraftServer : IAsyncDisposable
{
    private static readonly Action<ILogger, int, Exception?> _listening =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(MinecraftServer)),
            "VoXelSharp server listening on port {Port}.");

    private static readonly Action<ILogger, Exception?> _stopped =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2, nameof(MinecraftServer)),
            "Server stopped.");

    private static readonly Action<ILogger, Exception?> _fatalAcceptLoopError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3, nameof(MinecraftServer)),
            "Fatal accept-loop error, stopping server.");

    private static readonly Action<ILogger, Exception?> _clientError =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(4, nameof(MinecraftServer)),
            "Unexpected error while handling client.");

    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly TcpListener _listener;
    private readonly int _port;
    private readonly PacketRegistry _registry;
    private readonly IStatusJsonProvider _statusJson;
    private readonly IConfigurationDataProvider _configurationData;
    private readonly ILogger _logger;
    private Task? _acceptTask;
    private bool _started;
    private int _nextEntityId;

    /// <summary>
    /// Creates a server.
    /// </summary>
    /// <param name="registry">Shared packet registry registered with all supported codecs.</param>
    /// <param name="statusJson">Provider for the status response JSON.</param>
    /// <param name="logger">Server logger.</param>
    /// <param name="port">TCP port to bind (default 25565).</param>
    /// <param name="configurationData">Provider for the data sent during Configuration (defaults to the 26.3 set).</param>
    public MinecraftServer(
        PacketRegistry registry,
        IStatusJsonProvider statusJson,
        ILogger logger,
        int port = 25565,
        IConfigurationDataProvider? configurationData = null)
    {
        _registry = registry;
        _statusJson = statusJson;
        _configurationData = configurationData ?? new ServerConfigurationDataProvider();
        _logger = logger;
        _port = port;
        _listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
    }

    /// <summary>
    /// Starts the TCP listener and accept loop.
    /// </summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _listener.Start();
        _listening(_logger, _port, null);
        _acceptTask = Task.Run(AcceptLoopAsync);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    /// <summary>
    /// Gets the port the listener is bound to. Valid only after <see cref="Start"/>.
    /// </summary>
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>
    /// Stops the listener and waits for the accept loop.
    /// Active sessions complete asynchronously via the cancellation token.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _stoppingCts.Cancel();
        _listener.Stop();

        if (_acceptTask is not null)
        {
            try
            {
                await _acceptTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stopped(_logger, null);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stoppingCts.Token.IsCancellationRequested)
            {
                // Handler owns the TcpClient; no `using` here.
                TcpClient client = await _listener.AcceptTcpClientAsync(_stoppingCts.Token);

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }
        catch (OperationCanceledException) when (_stoppingCts.Token.IsCancellationRequested)
        {
            // StopAsync was called.
        }
        catch (Exception exception)
        {
            _fatalAcceptLoopError(_logger, exception);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                await using NetworkStream stream = client.GetStream();
                var pipe = new StreamDuplexPipe(stream);
                var stateMachine = new ConnectionStateMachine();
                int entityId = Interlocked.Increment(ref _nextEntityId);
                var playData = new ServerPlayDataProvider(entityId);
                using var session = new ConnectionSession(pipe, _registry, stateMachine, _statusJson, _configurationData, playData, _logger);

                await session.RunAsync(_stoppingCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown cancelled this session.
        }
        catch (Exception exception)
        {
            _clientError(_logger, exception);
        }
    }
}
