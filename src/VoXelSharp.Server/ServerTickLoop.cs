namespace VoXelSharp.Server;

/// <summary>
/// The server's fixed-rate tick clock (20 ticks per second). The loop increments
/// <see cref="CurrentTick"/> every 50 ms and invokes registered <see cref="Tick" /> handlers.
/// It is the single authoritative "game tick" source: movement timestamps, world simulation,
/// and (in M7) plugin update hooks all derive their tick from this loop rather than from
/// per-connection timers.
///
/// No gameplay handlers are registered yet; the loop is wired into the server lifecycle so
/// later M5 units (world ticking, block updates, movement broadcasts) can subscribe without
/// restructuring.
/// </summary>
public sealed class ServerTickLoop : IDisposable
{
    /// <summary>Minecraft's fixed tick rate, in ticks per second.</summary>
    public const int TicksPerSecond = 20;

    /// <summary>Duration of a single tick.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(1000.0 / TicksPerSecond);

    private readonly PeriodicTimer _timer = new(TickInterval);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loopTask;

    /// <summary>Raised once per tick with the new current tick number and a cancellation token.</summary>
    public event Func<long, CancellationToken, ValueTask>? Tick;

    /// <summary>The most recent tick number (0 until the loop runs).</summary>
    public long CurrentTick => Interlocked.Read(ref _currentTick);
    private long _currentTick;

    /// <summary>Starts the tick loop. Safe to call once; further calls are ignored.</summary>
    public void Start()
    {
        if (_loopTask is not null)
        {
            return;
        }

        _loopTask = Task.Run(RunLoopAsync);
    }

    /// <summary>
    /// Stops the loop and waits for it to finish. Safe to call multiple times.
    /// </summary>
    public async Task StopAsync()
    {
        _stopping.Cancel();
        _timer.Dispose();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The loop stops with the cancellation token.
            }
        }
    }

    /// <summary>Returns the current tick as a delegate, for injecting into sessions/tickables.</summary>
    public long GetCurrentTick() => CurrentTick;

    /// <inheritdoc />
    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    private async Task RunLoopAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
            {
                long tick = Interlocked.Increment(ref _currentTick);

                Func<long, CancellationToken, ValueTask>? handler = Tick;
                if (handler is not null)
                {
                    await handler(tick, _stopping.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // StopAsync requested; the loop exits cleanly.
        }
    }
}
