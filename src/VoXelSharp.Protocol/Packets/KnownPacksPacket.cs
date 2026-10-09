namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A single known pack entry sent in <see cref="KnownPacksPacket"/>.
/// </summary>
public sealed record KnownPack(string Namespace, string ID, string Version);

/// <summary>
/// Known Packs (configuration/clientbound, packet ID 15, official name "select_known_packs").
/// Tells the client which data packs the server uses, so the client can source registry data
/// from local packs instead of receiving it over the wire.
/// Field order per docs/data/packets.json: Known Packs (Prefixed Array) of
/// {Namespace: String(32767), ID: String(32767), Version: String(32767)}.
/// </summary>
public sealed class KnownPacksPacket : IPacket
{
    /// <summary>Maximum length for pack namespace, ID, or version in UTF-16 code units.</summary>
    public const int MaxPackFieldLength = 32767;

    public KnownPacksPacket(KnownPack[] packs)
    {
        ArgumentNullException.ThrowIfNull(packs);
        Packs = packs;
    }

    /// <summary>The list of known packs the server uses.</summary>
    public KnownPack[] Packs { get; }
}

/// <summary>Shared Known Packs body encoding; used by both directions.</summary>
internal static class KnownPacksIO
{
    public static void Encode(KnownPacksPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.Packs.Length);
        foreach (KnownPack pack in packet.Packs)
        {
            writer.WriteString(pack.Namespace, KnownPacksPacket.MaxPackFieldLength);
            writer.WriteString(pack.ID, KnownPacksPacket.MaxPackFieldLength);
            writer.WriteString(pack.Version, KnownPacksPacket.MaxPackFieldLength);
        }
    }

    public static KnownPacksPacket Decode(ref PacketReader reader)
    {
        int count = reader.ReadVarInt();
        if (count < 0)
        {
            throw new InvalidDataException($"Negative known-pack count ({count}).");
        }

        var packs = new KnownPack[count];
        for (int i = 0; i < count; i++)
        {
            string ns = reader.ReadString(KnownPacksPacket.MaxPackFieldLength);
            string id = reader.ReadString(KnownPacksPacket.MaxPackFieldLength);
            string version = reader.ReadString(KnownPacksPacket.MaxPackFieldLength);
            packs[i] = new KnownPack(ns, id, version);
        }

        return new KnownPacksPacket(packs);
    }
}

/// <summary>Codec for the clientbound Known Packs packet (select_known_packs, id 15).</summary>
public sealed class KnownPacksCodec : PacketCodec<KnownPacksPacket>
{
    /// <summary>Singleton access.</summary>
    public static KnownPacksCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 15;

    /// <inheritdoc />
    protected override void EncodeBody(KnownPacksPacket packet, PacketWriter writer) => KnownPacksIO.Encode(packet, writer);

    /// <inheritdoc />
    protected override KnownPacksPacket DecodeBody(ref PacketReader reader) => KnownPacksIO.Decode(ref reader);
}

/// <summary>Codec for the serverbound Known Packs packet (select_known_packs, id 7).</summary>
public sealed class ServerKnownPacksCodec : PacketCodec<KnownPacksPacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerKnownPacksCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 7;

    /// <inheritdoc />
    protected override void EncodeBody(KnownPacksPacket packet, PacketWriter writer) => KnownPacksIO.Encode(packet, writer);

    /// <inheritdoc />
    protected override KnownPacksPacket DecodeBody(ref PacketReader reader) => KnownPacksIO.Decode(ref reader);
}
