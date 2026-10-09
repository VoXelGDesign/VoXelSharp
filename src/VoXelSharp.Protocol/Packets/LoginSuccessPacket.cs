namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Login Success (login/clientbound, packet ID 2, official name "login_finished").
/// Field order per docs/data/packets.json:
///   Profile (Game Profile), Session ID (UUID).
/// When the client receives this, it transitions to the Configuration state.
/// </summary>
public sealed class LoginSuccessPacket : IPacket
{
    public LoginSuccessPacket(GameProfile profile, long sessionUuidMsb, long sessionUuidLsb)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;
        SessionUuidMsb = sessionUuidMsb;
        SessionUuidLsb = sessionUuidLsb;
    }

    /// <summary>The player's game profile (UUID, username, properties, signature).</summary>
    public GameProfile Profile { get; }

    /// <summary>Most significant 64 bits of the session UUID.</summary>
    public long SessionUuidMsb { get; }

    /// <summary>Least significant 64 bits of the session UUID.</summary>
    public long SessionUuidLsb { get; }
}

/// <summary>Codec for <see cref="LoginSuccessPacket"/>.</summary>
public sealed class LoginSuccessCodec : PacketCodec<LoginSuccessPacket>
{
    /// <summary>Singleton access.</summary>
    public static LoginSuccessCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Login;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 2;

    /// <inheritdoc />
    protected override void EncodeBody(LoginSuccessPacket packet, PacketWriter writer)
    {
        GameProfileIO.Encode(packet.Profile, writer);
        writer.WriteLong(packet.SessionUuidMsb);
        writer.WriteLong(packet.SessionUuidLsb);
    }

    /// <inheritdoc />
    protected override LoginSuccessPacket DecodeBody(ref PacketReader reader)
    {
        GameProfile profile = GameProfileIO.Decode(ref reader);
        long msb = reader.ReadLong();
        long lsb = reader.ReadLong();
        return new LoginSuccessPacket(profile, msb, lsb);
    }
}
