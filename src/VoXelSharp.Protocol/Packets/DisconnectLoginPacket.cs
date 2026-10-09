namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Disconnect (login) (login/clientbound, packet ID 0, official name "login_disconnect").
/// Field order per docs/data/packets.json: Reason (JSON Text Component).
/// The server uses this to reject a login attempt.
/// </summary>
public sealed class DisconnectLoginPacket : IPacket
{
    /// <summary>Maximum reason length in UTF-16 code units (32767 for JSON Text Components per vanilla).</summary>
    public const int MaxReasonLength = 32767;

    public DisconnectLoginPacket(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        if (reason.Length > MaxReasonLength)
        {
            throw new ArgumentException(
                $"Disconnect reason of {reason.Length} UTF-16 code units exceeds the limit of {MaxReasonLength}.",
                nameof(reason));
        }

        Reason = reason;
    }

    /// <summary>The JSON Text Component explaining the disconnect reason.</summary>
    public string Reason { get; }
}

/// <summary>Codec for <see cref="DisconnectLoginPacket"/>.</summary>
public sealed class DisconnectLoginCodec : PacketCodec<DisconnectLoginPacket>
{
    /// <summary>Singleton access.</summary>
    public static DisconnectLoginCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Login;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(DisconnectLoginPacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.Reason, DisconnectLoginPacket.MaxReasonLength);
    }

    /// <inheritdoc />
    protected override DisconnectLoginPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadString(DisconnectLoginPacket.MaxReasonLength));
}
