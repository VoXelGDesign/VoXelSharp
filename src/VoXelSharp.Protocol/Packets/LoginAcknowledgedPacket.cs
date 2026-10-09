namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Login Acknowledged (login/serverbound, packet ID 3, official name "login_acknowledged").
/// The client signals that it has finished processing the Login Success.
/// No fields (docs/data/packets.json).
/// </summary>
public sealed class LoginAcknowledgedPacket : IPacket
{
}

/// <summary>Codec for <see cref="LoginAcknowledgedPacket"/>.</summary>
public sealed class LoginAcknowledgedCodec : PacketCodec<LoginAcknowledgedPacket>
{
    /// <summary>Singleton access.</summary>
    public static LoginAcknowledgedCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Login;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 3;

    /// <inheritdoc />
    protected override void EncodeBody(LoginAcknowledgedPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override LoginAcknowledgedPacket DecodeBody(ref PacketReader reader) => new();
}
