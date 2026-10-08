namespace VoXelSharp.Protocol;

/// <summary>
/// The direction of a packet relative to the server, per docs/PROTOCOL.md Packet Identity.
/// Packet dispatch is keyed by state, direction, and packet ID; the underlying value of
/// this enum is an in-process dispatch key only and is never serialized to the wire.
/// </summary>
public enum PacketDirection
{
    /// <summary>Sent by the server to the client.</summary>
    Clientbound,

    /// <summary>Sent by the client to the server.</summary>
    Serverbound,
}
