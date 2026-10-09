namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Login Start (login/serverbound, packet ID 0, official name "hello").
/// Field order per docs/data/packets.json: Name (String 16), Player UUID (UUID).
/// The client begins authentication by sending its player name and UUID.
/// </summary>
public sealed class LoginStartPacket : IPacket
{
    /// <summary>Maximum player name length in UTF-16 code units.</summary>
    public const int MaxNameLength = 16;

    public LoginStartPacket(string name, long playerUuidMsb, long playerUuidLsb)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Player name of {name.Length} UTF-16 code units exceeds the limit of {MaxNameLength}.",
                nameof(name));
        }

        Name = name;
        PlayerUuidMsb = playerUuidMsb;
        PlayerUuidLsb = playerUuidLsb;
    }

    /// <summary>The player's username.</summary>
    public string Name { get; }

    /// <summary>Most significant 64 bits of the player's UUID.</summary>
    public long PlayerUuidMsb { get; }

    /// <summary>Least significant 64 bits of the player's UUID.</summary>
    public long PlayerUuidLsb { get; }
}

/// <summary>Codec for <see cref="LoginStartPacket"/>.</summary>
public sealed class LoginStartCodec : PacketCodec<LoginStartPacket>
{
    /// <summary>Singleton access.</summary>
    public static LoginStartCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Login;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(LoginStartPacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.Name, LoginStartPacket.MaxNameLength);
        writer.WriteLong(packet.PlayerUuidMsb);
        writer.WriteLong(packet.PlayerUuidLsb);
    }

    /// <inheritdoc />
    protected override LoginStartPacket DecodeBody(ref PacketReader reader)
    {
        string name = reader.ReadString(LoginStartPacket.MaxNameLength);
        long msb = reader.ReadLong();
        long lsb = reader.ReadLong();
        return new LoginStartPacket(name, msb, lsb);
    }
}
