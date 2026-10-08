namespace VoXelSharp.Protocol;

/// <summary>
/// State machine over the Minecraft protocol connection states, per docs/PROTOCOL.md.
/// The normal flow is Handshaking -&gt; Login -&gt; Configuration -&gt; Play; Status is an
/// optional branch from Handshaking with no documented onward transition (it ends in
/// disconnect). Configuration must not be skipped, so Login -&gt; Play is illegal.
/// The machine models only the documented state flow; packet IDs that trigger transitions
/// are UNVERIFIED and are not represented here.
/// </summary>
public sealed class ConnectionStateMachine
{
    private volatile ConnectionState _state = ConnectionState.Handshaking;

    /// <summary>The current connection state. Safe to read from any thread.</summary>
    public ConnectionState State => _state;

    /// <summary>
    /// Attempts to move the machine to <paramref name="next"/>.
    /// Transitions are driven by a single read loop per connection (see docs/ARCHITECTURE.md),
    /// so no lock is needed for the check-then-set.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the transition is legal per the documented protocol flow;
    /// <see langword="false"/> otherwise. On failure the current state is left unchanged.
    /// </returns>
    public bool TryTransitionTo(ConnectionState next)
    {
        if (!IsLegalTransition(State, next))
        {
            return false;
        }

        _state = next;
        return true;
    }

    /// <summary>
    /// Returns whether the (from, to) pair is a legal transition per docs/PROTOCOL.md.
    /// </summary>
    public static bool IsLegalTransition(ConnectionState from, ConnectionState to)
    {
        return (from, to) switch
        {
            // Handshaking selects the next state; Status and Login are the expected destinations.
            (ConnectionState.Handshaking, ConnectionState.Status) => true,
            (ConnectionState.Handshaking, ConnectionState.Login) => true,
            // Normal login flow: Handshaking -> Login -> Configuration -> Play.
            (ConnectionState.Login, ConnectionState.Configuration) => true,
            (ConnectionState.Configuration, ConnectionState.Play) => true,
            _ => false,
        };
    }
}
