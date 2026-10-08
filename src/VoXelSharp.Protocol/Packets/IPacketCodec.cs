namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A stateless codec for one packet type, used by <see cref="PacketRegistry"/> for dispatch.
/// </summary>
public interface IPacketCodec
{
    /// <summary>The connection state this packet belongs to.</summary>
    ConnectionState State { get; }

    /// <summary>The direction of this packet relative to the server.</summary>
    PacketDirection Direction { get; }

    /// <summary>The packet ID within <see cref="State"/> and <see cref="Direction"/>.</summary>
    int PacketId { get; }

    /// <summary>
    /// Encodes the packet as a complete payload: the packet ID VarInt followed by the body.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packet"/> is not of the codec's packet type.</exception>
    void Encode(IPacket packet, PacketWriter writer);

    /// <summary>
    /// Decodes the packet body.
    /// </summary>
    /// <param name="reader">Positioned at the first body byte (after the packet ID); advanced as the body is consumed.</param>
    /// <exception cref="InvalidDataException">Thrown when the body is malformed or truncated.</exception>
    IPacket Decode(ref PacketReader reader);
}
