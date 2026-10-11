using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server.Configuration;
using VoXelSharp.Server.Connections;
using VoXelSharp.Server.Play;
using VoXelSharp.Server.Status;
using VoXelSharp.World;

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

    private static readonly Action<ILogger, int, Exception?> _worldLoaded =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(5, nameof(MinecraftServer)),
            "Loaded {ColumnCount} changed chunk column(s) from the world save.");

    private static readonly Action<ILogger, int, Exception?> _worldSaved =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(6, nameof(MinecraftServer)),
            "Saved {ColumnCount} changed chunk column(s) to the world save.");

    private static readonly Action<ILogger, Exception?> _worldLoadFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(7, nameof(MinecraftServer)),
            "Could not load the world save (starting with a fresh world):");

    private static readonly Action<ILogger, Exception?> _worldSaveFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(8, nameof(MinecraftServer)),
            "Could not save the world:");

    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly TcpListener _listener;
    private readonly int _port;
    private readonly PacketRegistry _registry;
    private readonly IStatusJsonProvider _statusJson;
    private readonly IConfigurationDataProvider _configurationData;
    private readonly ILogger _logger;
    private readonly string? _worldDirectory;
    private readonly ServerTickLoop _tickLoop = new();
    private readonly FlatWorld _world = new();
    private readonly ServerEntityIds _entityIds = new();
    private Task? _acceptTask;
    private bool _started;

    /// <summary>
    /// Creates a server.
    /// </summary>
    /// <param name="registry">Shared packet registry registered with all supported codecs.</param>
    /// <param name="statusJson">Provider for the status response JSON.</param>
    /// <param name="logger">Server logger.</param>
    /// <param name="port">TCP port to bind (default 25565).</param>
    /// <param name="configurationData">Provider for the data sent during Configuration (defaults to the 26.3 set).</param>
    /// <param name="worldDirectory">
    /// Directory holding the world save file (<c>world.bin</c>). When set, the world is loaded at
    /// <see cref="Start"/> and flushed back on <see cref="StopAsync"/> (M6 persistence). When
    /// <see langword="null"/>, the server works with an in-memory only world.
    /// </param>
    public MinecraftServer(
        PacketRegistry registry,
        IStatusJsonProvider statusJson,
        ILogger logger,
        int port = 25565,
        IConfigurationDataProvider? configurationData = null,
        string? worldDirectory = null)
    {
        _registry = registry;
        _statusJson = statusJson;
        _configurationData = configurationData ?? new ServerConfigurationDataProvider();
        _logger = logger;
        _port = port;
        _worldDirectory = worldDirectory;
        _listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
    }

    /// <summary>
    /// Starts the TCP listener and accept loop. Loads the world save first when a world
    /// directory was configured, so clients see the persisted terrain.
    /// </summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        LoadWorld();
        _listener.Start();
        _tickLoop.Start();
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
    /// Stops the listener, waits for the accept loop and in-flight sessions, stops the tick
    /// loop, then flushes the world back to disk (when a world directory is configured).
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

        // Stop in-flight sessions via the cancellation token; they unwind asynchronously.
        await _tickLoop.StopAsync();

        SaveWorld();

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
                int entityId = _entityIds.Next();
                var playData = new ServerPlayDataProvider(entityId);
                using var session = new ConnectionSession(pipe, _registry, stateMachine, _statusJson, _configurationData, playData, _world, _logger, getTick: _tickLoop.GetCurrentTick, entityIds: _entityIds);

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

    /// <summary>Loads the world save (when configured) and applies it, logging instead of failing
    /// on corrupt or unreadable files — the server still boots with a fresh world.</summary>
    private void LoadWorld()
    {
        if (_worldDirectory is null)
        {
            return;
        }

        try
        {
            var changes = WorldFileStore.Load(_worldDirectory);
            _world.ApplyChanges(changes);
            _worldLoaded(_logger, changes.Count, null);
        }
        catch (InvalidDataException exception)
        {
            _worldLoadFailed(_logger, exception);
        }
        catch (IOException exception)
        {
            _worldLoadFailed(_logger, exception);
        }
    }

    /// <summary>Flushes the world diff to disk (when configured) without failing shutdown on I/O
    /// errors — they are logged instead.</summary>
    private void SaveWorld()
    {
        if (_worldDirectory is null)
        {
            return;
        }

        try
        {
            IReadOnlyList<WorldColumnChanges> changes = _world.ExportChanges();
            WorldFileStore.Save(_worldDirectory, changes);
            _worldSaved(_logger, changes.Count, null);
        }
        catch (InvalidDataException exception)
        {
            _worldSaveFailed(_logger, exception);
        }
        catch (IOException exception)
        {
            _worldSaveFailed(_logger, exception);
        }
    }
}
