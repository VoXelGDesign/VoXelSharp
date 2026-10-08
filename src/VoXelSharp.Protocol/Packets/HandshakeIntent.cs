namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// The Handshake Intent field (a VarInt on the wire), per docs/data/packets.json
/// (handshaking/serverbound/Handshake). 1 selects Status, 2 selects Login, and 3 (Transfer)
/// also transitions to Login.
/// </summary>
public enum HandshakeIntent
{
    /// <summary>Request the status flow (Status Request / Ping).</summary>
    Status = 1,

    /// <summary>Begin the login flow.</summary>
    Login = 2,

    /// <summary>Transfer to another server; still transitions to the Login state.</summary>
    Transfer = 3,
}
