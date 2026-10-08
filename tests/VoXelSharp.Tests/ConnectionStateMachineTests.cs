using VoXelSharp.Protocol;

namespace VoXelSharp.Tests;

public class ConnectionStateMachineTests
{
    [Fact]
    public void InitialStateIsHandshaking()
    {
        var machine = new ConnectionStateMachine();

        Assert.Equal(ConnectionState.Handshaking, machine.State);
    }

    [Fact]
    public void NormalLoginFlowReachesPlay()
    {
        var machine = new ConnectionStateMachine();

        Assert.True(machine.TryTransitionTo(ConnectionState.Login));
        Assert.True(machine.TryTransitionTo(ConnectionState.Configuration));
        Assert.True(machine.TryTransitionTo(ConnectionState.Play));

        Assert.Equal(ConnectionState.Play, machine.State);
    }

    [Fact]
    public void OptionalStatusBranchSucceeds()
    {
        var machine = new ConnectionStateMachine();

        Assert.True(machine.TryTransitionTo(ConnectionState.Status));
        Assert.Equal(ConnectionState.Status, machine.State);
    }

    [Fact]
    public void ConfigurationCannotBeSkipped()
    {
        var machine = new ConnectionStateMachine();

        Assert.True(machine.TryTransitionTo(ConnectionState.Login));

        Assert.False(machine.TryTransitionTo(ConnectionState.Play));
    }

    [Fact]
    public void FailedTransitionLeavesStateUnchanged()
    {
        var machine = new ConnectionStateMachine();
        Assert.True(machine.TryTransitionTo(ConnectionState.Login));

        Assert.False(machine.TryTransitionTo(ConnectionState.Play));
        Assert.Equal(ConnectionState.Login, machine.State);
    }

    [Fact]
    public void ReentryIntoSameStateIsIllegal()
    {
        var machine = new ConnectionStateMachine();

        Assert.False(machine.TryTransitionTo(ConnectionState.Handshaking));
    }

    [Fact]
    public void StatusHasNoOnwardTransition()
    {
        var machine = new ConnectionStateMachine();
        Assert.True(machine.TryTransitionTo(ConnectionState.Status));

        foreach (ConnectionState next in Enum.GetValues<ConnectionState>())
        {
            Assert.False(machine.TryTransitionTo(next));
        }

        Assert.Equal(ConnectionState.Status, machine.State);
    }

    [Fact]
    public void PlayIsTerminal()
    {
        var machine = new ConnectionStateMachine();
        Assert.True(machine.TryTransitionTo(ConnectionState.Login));
        Assert.True(machine.TryTransitionTo(ConnectionState.Configuration));
        Assert.True(machine.TryTransitionTo(ConnectionState.Play));

        foreach (ConnectionState next in Enum.GetValues<ConnectionState>())
        {
            Assert.False(machine.TryTransitionTo(next));
        }

        Assert.Equal(ConnectionState.Play, machine.State);
    }

    [Fact]
    public void LegalTransitionsAreExactlyTheDocumentedSet()
    {
        var legal = new HashSet<(ConnectionState, ConnectionState)>
        {
            (ConnectionState.Handshaking, ConnectionState.Status),
            (ConnectionState.Handshaking, ConnectionState.Login),
            (ConnectionState.Login, ConnectionState.Configuration),
            (ConnectionState.Configuration, ConnectionState.Play),
        };

        foreach (ConnectionState from in Enum.GetValues<ConnectionState>())
        {
            foreach (ConnectionState to in Enum.GetValues<ConnectionState>())
            {
                Assert.Equal(legal.Contains((from, to)), ConnectionStateMachine.IsLegalTransition(from, to));
            }
        }
    }
}
