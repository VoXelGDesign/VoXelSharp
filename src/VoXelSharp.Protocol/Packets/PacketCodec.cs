namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Base class for a stateless codec of a single typed packet, implementing
/// <see cref="IPacketCodec"/> with generic encode/decode.
/// </summary>
/// <typeparam name="TPacket">The concrete packet type handled by this codec.</typeparam>
public abstract class PacketCodec<TPacket> : IPacketCodec where TPacket : IPacket
{
    /// <inheritdoc />
    public abstract ConnectionState State { get; }

    /// <inheritdoc />
    public abstract PacketDirection Direction { get; }

    /// <inheritdoc />
    public abstract int PacketId { get; }

    /// <summary>
    /// Writes the packet body. The packet ID VarInt is written before this method is called.
    /// </summary>
    protected abstract void EncodeBody(TPacket packet, PacketWriter writer);

    /// <summary>
    /// Reads the packet body. The packet ID was already consumed by the caller.
    /// </summary>
    /// <param name="reader">Positioned at the first body byte (after the packet ID); advanced as the body is consumed.</param>
    /// <exception cref="InvalidDataException">Thrown when the body is malformed or truncated.</exception>
    protected abstract TPacket DecodeBody(ref PacketReader reader);

    /// <inheritdoc />
    public void Encode(IPacket packet, PacketWriter writer)
    {
        if (packet is not TPacket typed)
        {
            throw new ArgumentException(
                $"Packet of type '{packet.GetType().Name}' cannot be encoded by codec for '{typeof(TPacket).Name}'.",
                nameof(packet));
        }

        writer.WriteVarInt(PacketId);
        EncodeBody(typed, writer);
    }

    /// <inheritdoc />
    public IPacket Decode(ref PacketReader reader) => DecodeBody(ref reader);
}
