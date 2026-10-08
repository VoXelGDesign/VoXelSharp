namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Status Response (status/clientbound, packet ID 0, official name "status_response").
/// Field order per docs/data/packets.json: JSON Response (String 32767).
/// </summary>
public sealed class StatusResponsePacket : IPacket
{
    /// <summary>Maximum JSON response length in UTF-16 code units.</summary>
    public const int MaxJsonLength = 32767;

    public StatusResponsePacket(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxJsonLength)
        {
            throw new ArgumentException(
                $"Status JSON of {json.Length} UTF-16 code units exceeds the limit of {MaxJsonLength}.",
                nameof(json));
        }

        Json = json;
    }

    /// <summary>The server-list JSON (structure documented on the Server List Ping page).</summary>
    public string Json { get; }
}

/// <summary>Codec for <see cref="StatusResponsePacket"/>.</summary>
public sealed class StatusResponseCodec : PacketCodec<StatusResponsePacket>
{
    /// <summary>Singleton access.</summary>
    public static StatusResponseCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Status;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(StatusResponsePacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.Json, StatusResponsePacket.MaxJsonLength);
    }

    /// <inheritdoc />
    protected override StatusResponsePacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadString(StatusResponsePacket.MaxJsonLength));
}
