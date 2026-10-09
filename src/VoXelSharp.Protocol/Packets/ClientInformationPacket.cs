namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Chat mode values for <see cref="ClientInformationPacket.ChatMode"/>.
/// </summary>
public enum ClientChatMode
{
    Enabled = 0,
    CommandsOnly = 1,
    Hidden = 2,
}

/// <summary>
/// Main hand values for <see cref="ClientInformationPacket.MainHand"/>.
/// </summary>
public enum ClientMainHand
{
    Left = 0,
    Right = 1,
}

/// <summary>
/// Particle status values for <see cref="ClientInformationPacket.ParticleStatus"/>.
/// </summary>
public enum ClientParticleStatus
{
    All = 0,
    Decreased = 1,
    Minimal = 2,
}

/// <summary>
/// Client Information (configuration/serverbound, packet ID 0, official name "client_information").
/// Sent when the player connects or when settings are changed.
/// Field order per docs/data/packets.json:
/// Locale (String 16), View Distance (Byte), Chat Mode (VarInt Enum),
/// Chat Colors (Boolean), Displayed Skin Parts (Unsigned Byte),
/// Main Hand (VarInt Enum), Enable text filtering (Boolean),
/// Allow server listings (Boolean), Particle Status (VarInt Enum).
/// </summary>
public sealed class ClientInformationPacket : IPacket
{
    /// <summary>Maximum locale length in UTF-16 code units.</summary>
    public const int MaxLocaleLength = 16;

    public ClientInformationPacket(
        string locale,
        sbyte viewDistance,
        ClientChatMode chatMode,
        bool chatColors,
        byte displayedSkinParts,
        ClientMainHand mainHand,
        bool enableTextFiltering,
        bool allowServerListings,
        ClientParticleStatus particleStatus)
    {
        ArgumentNullException.ThrowIfNull(locale);

        Locale = locale;
        ViewDistance = viewDistance;
        ChatMode = chatMode;
        ChatColors = chatColors;
        DisplayedSkinParts = displayedSkinParts;
        MainHand = mainHand;
        EnableTextFiltering = enableTextFiltering;
        AllowServerListings = allowServerListings;
        ParticleStatus = particleStatus;
    }

    /// <summary>e.g. "en_GB".</summary>
    public string Locale { get; }

    /// <summary>Client-side render distance, in chunks.</summary>
    public sbyte ViewDistance { get; }

    /// <summary>Chat mode: 0 enabled, 1 commands only, 2 hidden.</summary>
    public ClientChatMode ChatMode { get; }

    /// <summary>"Colors" multiplayer setting.</summary>
    public bool ChatColors { get; }

    /// <summary>Bit mask for displayed skin parts (bits 0-6: cape, jacket, sleeves, pants, hat).</summary>
    public byte DisplayedSkinParts { get; }

    /// <summary>0: Left, 1: Right.</summary>
    public ClientMainHand MainHand { get; }

    /// <summary>Enables filtering of text on signs and written book titles.</summary>
    public bool EnableTextFiltering { get; }

    /// <summary>Whether the player allows their name to appear in the server player list.</summary>
    public bool AllowServerListings { get; }

    /// <summary>0: all, 1: decreased, 2: minimal.</summary>
    public ClientParticleStatus ParticleStatus { get; }
}

/// <summary>Shared Client Information body encoding; identical in Configuration and Play.</summary>
internal static class ClientInformationIO
{
    public static void Encode(ClientInformationPacket packet, PacketWriter writer)
    {
        writer.WriteString(packet.Locale, ClientInformationPacket.MaxLocaleLength);
        writer.WriteByte((byte)packet.ViewDistance);
        writer.WriteVarInt((int)packet.ChatMode);
        writer.WriteBoolean(packet.ChatColors);
        writer.WriteByte(packet.DisplayedSkinParts);
        writer.WriteVarInt((int)packet.MainHand);
        writer.WriteBoolean(packet.EnableTextFiltering);
        writer.WriteBoolean(packet.AllowServerListings);
        writer.WriteVarInt((int)packet.ParticleStatus);
    }

    public static ClientInformationPacket Decode(ref PacketReader reader)
    {
        string locale = reader.ReadString(ClientInformationPacket.MaxLocaleLength);
        sbyte viewDistance = (sbyte)reader.ReadByte();

        int chatModeValue = reader.ReadVarInt();
        if (chatModeValue < (int)ClientChatMode.Enabled || chatModeValue > (int)ClientChatMode.Hidden)
        {
            throw new InvalidDataException($"Invalid chat mode value ({chatModeValue}).");
        }

        var chatMode = (ClientChatMode)chatModeValue;
        bool chatColors = reader.ReadBoolean();
        byte skinParts = reader.ReadByte();

        int mainHandValue = reader.ReadVarInt();
        if (mainHandValue < (int)ClientMainHand.Left || mainHandValue > (int)ClientMainHand.Right)
        {
            throw new InvalidDataException($"Invalid main hand value ({mainHandValue}).");
        }

        var mainHand = (ClientMainHand)mainHandValue;
        bool filter = reader.ReadBoolean();
        bool listings = reader.ReadBoolean();

        int particleValue = reader.ReadVarInt();
        if (particleValue < (int)ClientParticleStatus.All || particleValue > (int)ClientParticleStatus.Minimal)
        {
            throw new InvalidDataException($"Invalid particle status value ({particleValue}).");
        }

        var particleStatus = (ClientParticleStatus)particleValue;

        return new ClientInformationPacket(
            locale, viewDistance, chatMode, chatColors, skinParts,
            mainHand, filter, listings, particleStatus);
    }
}

/// <summary>
/// Codec for the Configuration serverbound Client Information packet (id 0).
/// </summary>
public sealed class ClientInformationCodec : PacketCodec<ClientInformationPacket>
{
    /// <summary>Singleton access.</summary>
    public static ClientInformationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Configuration;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(ClientInformationPacket packet, PacketWriter writer)
        => ClientInformationIO.Encode(packet, writer);

    /// <inheritdoc />
    protected override ClientInformationPacket DecodeBody(ref PacketReader reader)
        => ClientInformationIO.Decode(ref reader);
}

/// <summary>
/// Codec for the Play serverbound Client Information packet (id 14). The client resends its
/// settings when they change; the wire body is identical to the Configuration variant.
/// Verified as protocol_id 14 against the Mojang data-generator report.
/// </summary>
public sealed class PlayClientInformationCodec : PacketCodec<ClientInformationPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayClientInformationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 14;

    /// <inheritdoc />
    protected override void EncodeBody(ClientInformationPacket packet, PacketWriter writer)
        => ClientInformationIO.Encode(packet, writer);

    /// <inheritdoc />
    protected override ClientInformationPacket DecodeBody(ref PacketReader reader)
        => ClientInformationIO.Decode(ref reader);
}
