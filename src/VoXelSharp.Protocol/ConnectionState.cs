namespace VoXelSharp.Protocol;

/// <summary>
/// The Minecraft protocol connection states, per docs/PROTOCOL.md.
/// The normal login flow is Handshaking -&gt; Login -&gt; Configuration -&gt; Play;
/// Status is an optional branch from Handshaking.
/// Packet dispatch is keyed by state, direction, and packet ID; the underlying value of
/// this enum is an in-process dispatch key only and is never serialized to the wire.
/// </summary>
public enum ConnectionState
{
    /// <summary>Contains the initial handshake packet; the handshake selects the next state.</summary>
    Handshaking,

    /// <summary>Optional branch from Handshaking; handles the Status and Ping flows.</summary>
    Status,

    /// <summary>Handles authentication and login negotiation.</summary>
    Login,

    /// <summary>First-class state; sends data the client needs before entering Play.</summary>
    Configuration,

    /// <summary>The gameplay protocol state.</summary>
    Play,
}
