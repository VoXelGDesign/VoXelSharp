namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Handshake (handshaking/serverbound, packet ID 0, official name "intention").
/// Field order per docs/data/packets.json:
/// Protocol Version (VarInt), Server Address (String 255), Server Port (Unsigned Short),
/// Intent (VarInt Enum: 1 Status, 2 Login, 3 Transfer).
/// </summary>
public sealed class HandshakePacket : IPacket
{
    /// <summary>Maximum server address length in UTF-16 code units.</summary>
    public const int MaxServerAddressLength = 255;

    public HandshakePacket(int protocolVersion, string serverAddress, ushort serverPort, HandshakeIntent intent)
    {
        ArgumentNullException.ThrowIfNull(serverAddress);
        if (serverAddress.Length > MaxServerAddressLength)
        {
            throw new ArgumentException(
                $"Server address of {serverAddress.Length} UTF-16 code units exceeds the limit of {MaxServerAddressLength}.",
                nameof(serverAddress));
        }

        ProtocolVersion = protocolVersion;
        ServerAddress = serverAddress;
        ServerPort = serverPort;
        Intent = intent;
    }

    /// <summary>The protocol version the client is using, e.g. 777 for Minecraft 26.3.</summary>
    public int ProtocolVersion { get; }

    /// <summary>The hostname or IP used to connect, e.g. "localhost".</summary>
    public string ServerAddress { get; }

    /// <summary>The port used to connect (the vanilla server does not use this).</summary>
    public ushort ServerPort { get; }

    /// <summary>The requested next protocol flow (Status, Login, or Transfer).</summary>
    public HandshakeIntent Intent { get; }
}

/// <summary>Codec for <see cref="HandshakePacket"/>.</summary>
public sealed class HandshakeCodec : PacketCodec<HandshakePacket>
{
    /// <summary>Singleton access.</summary>
    public static HandshakeCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Handshaking;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(HandshakePacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.ProtocolVersion);
        writer.WriteString(packet.ServerAddress, HandshakePacket.MaxServerAddressLength);
        writer.WriteUnsignedShort(packet.ServerPort);
        writer.WriteVarInt((int)packet.Intent);
    }

    /// <inheritdoc />
    protected override HandshakePacket DecodeBody(ref PacketReader reader)
    {
        int protocolVersion = reader.ReadVarInt();
        string serverAddress = reader.ReadString(HandshakePacket.MaxServerAddressLength);
        ushort serverPort = reader.ReadUnsignedShort();

        int intent = reader.ReadVarInt();
        if (intent is < (int)HandshakeIntent.Status or > (int)HandshakeIntent.Transfer)
        {
            // VarInt Enum: an invalid value is a protocol violation (docs/data + Data types page).
            throw new InvalidDataException($"Handshake intent {intent} is not a documented value (1 Status, 2 Login, 3 Transfer).");
        }

        return new HandshakePacket(protocolVersion, serverAddress, serverPort, (HandshakeIntent)intent);
    }
}
