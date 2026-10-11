using VoXelSharp.Server;

namespace VoXelSharp.Tests;

public class ServerTickLoopTests
{
    [Fact]
    public async Task LoopAdvancesTickCount()
    {
        using var loop = new ServerTickLoop();
        long ticksObserved = 0;
        loop.Tick += (tick, _) =>
        {
            Interlocked.Exchange(ref ticksObserved, tick);
            return ValueTask.CompletedTask;
        };

        loop.Start();
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
            Assert.True(Volatile.Read(ref ticksObserved) >= 1, "Tick loop did not tick within 300 ms.");
        }
        finally
        {
            await loop.StopAsync();
        }
    }

    [Fact]
    public async Task StopHaltsTicking()
    {
        using var loop = new ServerTickLoop();
        long ticksObserved = 0;
        loop.Tick += (tick, _) =>
        {
            Interlocked.Exchange(ref ticksObserved, tick);
            return ValueTask.CompletedTask;
        };

        loop.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await loop.StopAsync();
        long atStop = Volatile.Read(ref ticksObserved);

        await Task.Delay(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
        long afterStop = Volatile.Read(ref ticksObserved);

        Assert.Equal(atStop, afterStop);
    }

    [Fact]
    public async Task TickIsCalledAfterStartAndBeforeStop()
    {
        using var loop = new ServerTickLoop();
        int calls = 0;
        loop.Tick += (_, _) =>
        {
            Interlocked.Increment(ref calls);
            return ValueTask.CompletedTask;
        };

        loop.Start();
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        await loop.StopAsync();

        Assert.True(Volatile.Read(ref calls) >= 1);
    }
}
