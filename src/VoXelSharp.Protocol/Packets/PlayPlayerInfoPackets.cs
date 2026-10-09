using VoXelSharp.Protocol.Nbt;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A 128-bit UUID split into two 64-bit halves (most significant first), the wire
/// representation of the UUID data type (docs/data/wire_types.md, wiki 2026-10-09).
/// </summary>
public readonly record struct PlayerUuid(long Msb, long Lsb);

/// <summary>
/// Action masks for Player Info Update (play/clientbound, id 71). A set bit in the Actions
/// byte means the corresponding action data is present for every player in the Players array,
/// in ascending mask order.
/// Source: docs/data/_raw/_packets.raw.wikitext.txt (wiki, 2026-10-09).
/// </summary>
[Flags]
public enum PlayerInfoActions : byte
{
    None = 0,
    AddPlayer = 0x01,
    InitializeChat = 0x02,
    UpdateGameMode = 0x04,
    UpdateListed = 0x08,
    UpdateLatency = 0x10,
    UpdateDisplayName = 0x20,
    UpdateListPriority = 0x40,
    UpdateHat = 0x80,
}

/// <summary>Add Player action payload: profile name and game-profile properties.</summary>
public sealed record PlayerInfoAddPlayerData(string Name, ProfileProperty[] Properties);

/// <summary>
/// Initialize Chat action payload. Wrapped in a Prefixed Optional: when present, all four
/// fields are written (UUID, expiry, key, signature).
/// </summary>
public sealed record PlayerInfoChatSessionData(
    long SessionIdMsb,
    long SessionIdLsb,
    long PublicKeyExpiry,
    byte[] EncodedPublicKey,
    byte[] PublicKeySignature)
{
    /// <summary>Maximum encoded public-key size in bytes (docs/data/packets.json wiki capture).</summary>
    public const int MaxEncodedPublicKeyLength = 512;

    /// <summary>Maximum public-key signature size in bytes.</summary>
    public const int MaxPublicKeySignatureLength = 4096;
}

/// <summary>
/// One player in a Player Info Update. Fields that are not present for the packet's Action
/// bitmask are null. AddPlayer carries the game-profile properties (same layout as
/// <see cref="GameProfile.Properties"/>).
/// </summary>
public sealed record PlayerInfoEntryData(
    long UuidMsb,
    long UuidLsb,
    PlayerInfoAddPlayerData? AddPlayer,
    PlayerInfoChatSessionData? ChatSession,
    int? GameMode,
    bool? Listed,
    int? Ping,
    NbtNode? DisplayName,
    int? ListPriority,
    bool? HatVisible);

/// <summary>
/// Player Info Update (play/clientbound, packet ID 71, official name "player_info_update").
/// Sent to add players to the tab list and/or update their properties. Field order per
/// docs/data/packets.json: Actions (Unsigned Byte), Players (Prefixed Array of {UUID,
/// Player Actions}). Verified as protocol_id 71 against the Mojang data-generator report.
/// </summary>
public sealed class PlayerInfoUpdatePacket : IPacket
{
    /// <summary>
    /// Defensive decode cap for the Players array. The protocol specifies no upper bound, so
    /// this implementation limit is documented here before use (AGENTS security-limit rule).
    /// </summary>
    public const int MaxPlayersCount = 4096;

    /// <summary>Maximum length of the Add Player name field (String 16).</summary>
    public const int MaxNameLength = 16;

    public PlayerInfoUpdatePacket(PlayerInfoActions actions, PlayerInfoEntryData[] players)
    {
        ArgumentNullException.ThrowIfNull(players);

        Actions = actions;
        Players = players;
    }

    public PlayerInfoActions Actions { get; }

    public PlayerInfoEntryData[] Players { get; }
}

/// <summary>Codec for <see cref="PlayerInfoUpdatePacket"/>.</summary>
public sealed class PlayerInfoUpdateCodec : PacketCodec<PlayerInfoUpdatePacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayerInfoUpdateCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 71;

    /// <inheritdoc />
    protected override void EncodeBody(PlayerInfoUpdatePacket packet, PacketWriter writer)
    {
        writer.WriteByte((byte)packet.Actions);
        writer.WriteVarInt(packet.Players.Length);
        foreach (PlayerInfoEntryData player in packet.Players)
        {
            writer.WriteLong(player.UuidMsb);
            writer.WriteLong(player.UuidLsb);
            WriteActions(packet.Actions, player, writer);
        }
    }

    private static void WriteActions(PlayerInfoActions actions, PlayerInfoEntryData player, PacketWriter writer)
    {
        if (actions.HasFlag(PlayerInfoActions.AddPlayer))
        {
            if (player.AddPlayer is null)
            {
                throw new InvalidDataException("Add Player action is set but the entry carries no profile.");
            }

            writer.WriteString(player.AddPlayer.Name, PlayerInfoUpdatePacket.MaxNameLength);
            GameProfileIO.WriteProperties(player.AddPlayer.Properties, writer);
        }

        if (actions.HasFlag(PlayerInfoActions.InitializeChat))
        {
            writer.WriteBoolean(player.ChatSession is not null);
            if (player.ChatSession is PlayerInfoChatSessionData chat)
            {
                writer.WriteLong(chat.SessionIdMsb);
                writer.WriteLong(chat.SessionIdLsb);
                writer.WriteLong(chat.PublicKeyExpiry);
                writer.WritePrefixedByteArray(chat.EncodedPublicKey);
                writer.WritePrefixedByteArray(chat.PublicKeySignature);
            }
        }

        if (actions.HasFlag(PlayerInfoActions.UpdateGameMode))
        {
            if (player.GameMode is not int gameMode)
            {
                throw new InvalidDataException("Update Game Mode action is set but no game mode was provided.");
            }

            writer.WriteVarInt(gameMode);
        }

        if (actions.HasFlag(PlayerInfoActions.UpdateListed))
        {
            if (player.Listed is not bool listed)
            {
                throw new InvalidDataException("Update Listed action is set but no value was provided.");
            }

            writer.WriteBoolean(listed);
        }

        if (actions.HasFlag(PlayerInfoActions.UpdateLatency))
        {
            if (player.Ping is not int ping)
            {
                throw new InvalidDataException("Update Latency action is set but no ping was provided.");
            }

            writer.WriteVarInt(ping);
        }

        if (actions.HasFlag(PlayerInfoActions.UpdateDisplayName))
        {
            writer.WriteBoolean(player.DisplayName is not null);
            if (player.DisplayName is NbtNode displayName)
            {
                NbtWriter.WriteRootNode(writer, displayName);
            }
        }

        if (actions.HasFlag(PlayerInfoActions.UpdateListPriority))
        {
            if (player.ListPriority is not int priority)
            {
                throw new InvalidDataException("Update List Priority action is set but no priority was provided.");
            }

            writer.WriteVarInt(priority);
        }

        if (actions.HasFlag(PlayerInfoActions.UpdateHat))
        {
            if (player.HatVisible is not bool visible)
            {
                throw new InvalidDataException("Update Hat action is set but no value was provided.");
            }

            writer.WriteBoolean(visible);
        }
    }

    /// <inheritdoc />
    protected override PlayerInfoUpdatePacket DecodeBody(ref PacketReader reader)
    {
        byte actionsValue = reader.ReadByte();
        var actions = (PlayerInfoActions)actionsValue;

        int playerCount = reader.ReadVarInt();
        if (playerCount < 0 || playerCount > PlayerInfoUpdatePacket.MaxPlayersCount)
        {
            throw new InvalidDataException(
                $"Player count {playerCount} is out of range (0..{PlayerInfoUpdatePacket.MaxPlayersCount}).");
        }

        var players = new PlayerInfoEntryData[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            long msb = reader.ReadLong();
            long lsb = reader.ReadLong();
            players[i] = new PlayerInfoEntryData(
                msb,
                lsb,
                ReadAddPlayer(actions, ref reader),
                ReadChatSession(actions, ref reader),
                ReadGameMode(actions, ref reader),
                ReadListed(actions, ref reader),
                ReadPing(actions, ref reader),
                ReadDisplayName(actions, ref reader),
                ReadPriority(actions, ref reader),
                ReadHat(actions, ref reader));
        }

        return new PlayerInfoUpdatePacket(actions, players);
    }

    private static PlayerInfoAddPlayerData? ReadAddPlayer(PlayerInfoActions actions, ref PacketReader reader)
    {
        if (!actions.HasFlag(PlayerInfoActions.AddPlayer))
        {
            return null;
        }

        string name = reader.ReadString(PlayerInfoUpdatePacket.MaxNameLength);
        ProfileProperty[] properties = GameProfileIO.ReadProperties(ref reader);
        return new PlayerInfoAddPlayerData(name, properties);
    }

    private static PlayerInfoChatSessionData? ReadChatSession(PlayerInfoActions actions, ref PacketReader reader)
    {
        if (!actions.HasFlag(PlayerInfoActions.InitializeChat))
        {
            return null;
        }

        if (!reader.ReadBoolean())
        {
            return null;
        }

        long sessionMsb = reader.ReadLong();
        long sessionLsb = reader.ReadLong();
        long expiry = reader.ReadLong();
        byte[] key = ReadBoundedPrefixedBytes(ref reader, PlayerInfoChatSessionData.MaxEncodedPublicKeyLength, "encoded public key");
        byte[] signature = ReadBoundedPrefixedBytes(ref reader, PlayerInfoChatSessionData.MaxPublicKeySignatureLength, "public key signature");
        return new PlayerInfoChatSessionData(sessionMsb, sessionLsb, expiry, key, signature);
    }

    private static int? ReadGameMode(PlayerInfoActions actions, ref PacketReader reader)
    {
        if (!actions.HasFlag(PlayerInfoActions.UpdateGameMode))
        {
            return null;
        }

        int gameMode = reader.ReadVarInt();
        if (gameMode < (int)PlayerGameMode.Survival || gameMode > (int)PlayerGameMode.Spectator)
        {
            throw new InvalidDataException($"Invalid game mode value ({gameMode}).");
        }

        return gameMode;
    }

    private static bool? ReadListed(PlayerInfoActions actions, ref PacketReader reader)
    {
        return actions.HasFlag(PlayerInfoActions.UpdateListed) ? reader.ReadBoolean() : null;
    }

    private static int? ReadPing(PlayerInfoActions actions, ref PacketReader reader)
    {
        return actions.HasFlag(PlayerInfoActions.UpdateLatency) ? reader.ReadVarInt() : null;
    }

    private static NbtNode? ReadDisplayName(PlayerInfoActions actions, ref PacketReader reader)
    {
        if (!actions.HasFlag(PlayerInfoActions.UpdateDisplayName))
        {
            return null;
        }

        return reader.ReadBoolean() ? NbtReader.ReadRootNode(ref reader) : null;
    }

    private static int? ReadPriority(PlayerInfoActions actions, ref PacketReader reader)
    {
        return actions.HasFlag(PlayerInfoActions.UpdateListPriority) ? reader.ReadVarInt() : null;
    }

    private static bool? ReadHat(PlayerInfoActions actions, ref PacketReader reader)
    {
        return actions.HasFlag(PlayerInfoActions.UpdateHat) ? reader.ReadBoolean() : null;
    }

    private static byte[] ReadBoundedPrefixedBytes(ref PacketReader reader, int maxLength, string fieldName)
    {
        int length = reader.ReadVarInt();
        if (length < 0 || length > maxLength)
        {
            throw new InvalidDataException($"{fieldName} length ({length}) is out of range (0..{maxLength}).");
        }

        byte[] data = reader.ReadBytes(length);
        if (data.Length != length)
        {
            throw new InvalidDataException($"Truncated {fieldName}.");
        }

        return data;
    }
}

/// <summary>
/// Player Info Remove (play/clientbound, packet ID 70, official name "player_info_remove").
/// Sent to remove players from the client's tab list. Field: UUIDs (Prefixed Array of UUID).
/// Verified as protocol_id 70 against the Mojang data-generator report.
/// </summary>
public sealed class PlayerInfoRemovePacket : IPacket
{
    /// <summary>Defensive decode cap for the UUID array (no protocol-specified bound).</summary>
    public const int MaxUuidCount = 4096;

    public PlayerInfoRemovePacket(PlayerUuid[] uuids)
    {
        ArgumentNullException.ThrowIfNull(uuids);
        Uuids = uuids;
    }

    public PlayerUuid[] Uuids { get; }
}

/// <summary>Codec for <see cref="PlayerInfoRemovePacket"/>.</summary>
public sealed class PlayerInfoRemoveCodec : PacketCodec<PlayerInfoRemovePacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayerInfoRemoveCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 70;

    /// <inheritdoc />
    protected override void EncodeBody(PlayerInfoRemovePacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.Uuids.Length);
        foreach (PlayerUuid uuid in packet.Uuids)
        {
            writer.WriteLong(uuid.Msb);
            writer.WriteLong(uuid.Lsb);
        }
    }

    /// <inheritdoc />
    protected override PlayerInfoRemovePacket DecodeBody(ref PacketReader reader)
    {
        int count = reader.ReadVarInt();
        if (count < 0 || count > PlayerInfoRemovePacket.MaxUuidCount)
        {
            throw new InvalidDataException(
                $"UUID count {count} is out of range (0..{PlayerInfoRemovePacket.MaxUuidCount}).");
        }

        var uuids = new PlayerUuid[count];
        for (int i = 0; i < count; i++)
        {
            uuids[i] = new PlayerUuid(reader.ReadLong(), reader.ReadLong());
        }

        return new PlayerInfoRemovePacket(uuids);
    }
}
