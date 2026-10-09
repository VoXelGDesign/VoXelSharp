namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Game mode values for <see cref="LoginPlayPacket.GameMode"/> (docs/data/packets.json).
/// </summary>
public enum PlayerGameMode
{
    Survival = 0,
    Creative = 1,
    Adventure = 2,
    Spectator = 3,
}

/// <summary>An optional (prefixed) game mode; 0 means undefined, otherwise value is mode + 1.</summary>
public enum OptionalPlayerGameMode
{
    Undefined = 0,
    Survival = 1,
    Creative = 2,
    Adventure = 3,
    Spectator = 4,
}

/// <summary>
/// Login (play) (play/clientbound, packet ID 50, official name "login").
/// The first packet sent after the player enters Play; it defines the player's entity,
/// the dimension, and world-wide settings.
/// Field order (23 fields) per docs/data/packets.json, verified against the Mojang
/// data-generator report (protocol_id 50).
/// </summary>
public sealed class LoginPlayPacket : IPacket
{
    /// <summary>Maximum length for an Identifier (String) field in this packet (protocol String limit).</summary>
    public const int MaxIdentifierLength = 32767;

    public LoginPlayPacket(
        int entityId,
        bool isHardcore,
        string[] dimensionNames,
        int maxPlayers,
        int viewDistance,
        int simulationDistance,
        bool reducedDebugInfo,
        bool enableRespawnScreen,
        bool doLimitedCrafting,
        int dimensionType,
        string dimensionName,
        long hashedSeed,
        PlayerGameMode gameMode,
        OptionalPlayerGameMode previousGameMode,
        bool isDebug,
        bool isFlat,
        bool hasDeathLocation,
        string? deathDimensionName,
        BlockPosition? deathLocation,
        int portalCooldown,
        int seaLevel,
        bool onlineMode,
        bool enforcesSecureChat)
    {
        ArgumentNullException.ThrowIfNull(dimensionNames);
        ArgumentNullException.ThrowIfNull(dimensionName);
        EntityId = entityId;
        IsHardcore = isHardcore;
        DimensionNames = dimensionNames;
        MaxPlayers = maxPlayers;
        ViewDistance = viewDistance;
        SimulationDistance = simulationDistance;
        ReducedDebugInfo = reducedDebugInfo;
        EnableRespawnScreen = enableRespawnScreen;
        DoLimitedCrafting = doLimitedCrafting;
        DimensionType = dimensionType;
        DimensionName = dimensionName;
        HashedSeed = hashedSeed;
        GameMode = gameMode;
        PreviousGameMode = previousGameMode;
        IsDebug = isDebug;
        IsFlat = isFlat;
        HasDeathLocation = hasDeathLocation;
        DeathDimensionName = deathDimensionName;
        DeathLocation = deathLocation;
        PortalCooldown = portalCooldown;
        SeaLevel = seaLevel;
        OnlineMode = onlineMode;
        EnforcesSecureChat = enforcesSecureChat;
    }

    /// <summary>The player's entity ID (EID).</summary>
    public int EntityId { get; }

    public bool IsHardcore { get; }

    /// <summary>Identifiers for all dimensions on the server.</summary>
    public string[] DimensionNames { get; }

    public int MaxPlayers { get; }

    /// <summary>Render distance in chunks (2-32).</summary>
    public int ViewDistance { get; }

    public int SimulationDistance { get; }

    public bool ReducedDebugInfo { get; }

    public bool EnableRespawnScreen { get; }

    public bool DoLimitedCrafting { get; }

    /// <summary>ID of the dimension type in the minecraft:dimension_type registry.</summary>
    public int DimensionType { get; }

    /// <summary>Name of the dimension the player spawns into.</summary>
    public string DimensionName { get; }

    public long HashedSeed { get; }

    public PlayerGameMode GameMode { get; }

    public OptionalPlayerGameMode PreviousGameMode { get; }

    public bool IsDebug { get; }

    public bool IsFlat { get; }

    /// <summary>If true, <see cref="DeathDimensionName"/> and <see cref="DeathLocation"/> are present.</summary>
    public bool HasDeathLocation { get; }

    public string? DeathDimensionName { get; }

    public BlockPosition? DeathLocation { get; }

    public int PortalCooldown { get; }

    public int SeaLevel { get; }

    public bool OnlineMode { get; }

    public bool EnforcesSecureChat { get; }
}

/// <summary>Codec for <see cref="LoginPlayPacket"/>.</summary>
public sealed class LoginPlayCodec : PacketCodec<LoginPlayPacket>
{
    /// <summary>Singleton access.</summary>
    public static LoginPlayCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 50;

    /// <inheritdoc />
    protected override void EncodeBody(LoginPlayPacket packet, PacketWriter writer)
    {
        writer.WriteInt(packet.EntityId);
        writer.WriteBoolean(packet.IsHardcore);

        writer.WriteVarInt(packet.DimensionNames.Length);
        foreach (string name in packet.DimensionNames)
        {
            writer.WriteString(name, LoginPlayPacket.MaxIdentifierLength);
        }

        writer.WriteVarInt(packet.MaxPlayers);
        writer.WriteVarInt(packet.ViewDistance);
        writer.WriteVarInt(packet.SimulationDistance);
        writer.WriteBoolean(packet.ReducedDebugInfo);
        writer.WriteBoolean(packet.EnableRespawnScreen);
        writer.WriteBoolean(packet.DoLimitedCrafting);
        writer.WriteVarInt(packet.DimensionType);
        writer.WriteString(packet.DimensionName, LoginPlayPacket.MaxIdentifierLength);
        writer.WriteLong(packet.HashedSeed);
        writer.WriteVarInt((int)packet.GameMode);
        writer.WriteVarInt((int)packet.PreviousGameMode);
        writer.WriteBoolean(packet.IsDebug);
        writer.WriteBoolean(packet.IsFlat);
        writer.WriteBoolean(packet.HasDeathLocation);

        if (packet.HasDeathLocation)
        {
            if (packet.DeathDimensionName is null)
            {
                throw new InvalidDataException("Has death location is true but the death dimension name is missing.");
            }

            if (packet.DeathLocation is null)
            {
                throw new InvalidDataException("Has death location is true but the death location is missing.");
            }

            writer.WriteString(packet.DeathDimensionName, LoginPlayPacket.MaxIdentifierLength);
            writer.WritePosition(packet.DeathLocation.Value);
        }

        writer.WriteVarInt(packet.PortalCooldown);
        writer.WriteVarInt(packet.SeaLevel);
        writer.WriteBoolean(packet.OnlineMode);
        writer.WriteBoolean(packet.EnforcesSecureChat);
    }

    /// <inheritdoc />
    protected override LoginPlayPacket DecodeBody(ref PacketReader reader)
    {
        int entityId = reader.ReadInt();
        bool isHardcore = reader.ReadBoolean();

        int dimensionCount = reader.ReadVarInt();
        if (dimensionCount < 0)
        {
            throw new InvalidDataException($"Negative dimension name count ({dimensionCount}).");
        }

        var dimensionNames = new string[dimensionCount];
        for (int i = 0; i < dimensionCount; i++)
        {
            dimensionNames[i] = reader.ReadString(LoginPlayPacket.MaxIdentifierLength);
        }

        int maxPlayers = reader.ReadVarInt();
        int viewDistance = reader.ReadVarInt();
        int simulationDistance = reader.ReadVarInt();
        bool reducedDebugInfo = reader.ReadBoolean();
        bool enableRespawnScreen = reader.ReadBoolean();
        bool doLimitedCrafting = reader.ReadBoolean();
        int dimensionType = reader.ReadVarInt();
        string dimensionName = reader.ReadString(LoginPlayPacket.MaxIdentifierLength);
        long hashedSeed = reader.ReadLong();

        int gameModeValue = reader.ReadVarInt();
        if (gameModeValue < (int)PlayerGameMode.Survival || gameModeValue > (int)PlayerGameMode.Spectator)
        {
            throw new InvalidDataException($"Invalid game mode value ({gameModeValue}).");
        }

        var gameMode = (PlayerGameMode)gameModeValue;

        int previousGameModeValue = reader.ReadVarInt();
        if (previousGameModeValue < (int)OptionalPlayerGameMode.Undefined || previousGameModeValue > (int)OptionalPlayerGameMode.Spectator)
        {
            throw new InvalidDataException($"Invalid previous game mode value ({previousGameModeValue}).");
        }

        var previousGameMode = (OptionalPlayerGameMode)previousGameModeValue;

        bool isDebug = reader.ReadBoolean();
        bool isFlat = reader.ReadBoolean();
        bool hasDeathLocation = reader.ReadBoolean();

        string? deathDimensionName = null;
        BlockPosition? deathLocation = null;
        if (hasDeathLocation)
        {
            deathDimensionName = reader.ReadString(LoginPlayPacket.MaxIdentifierLength);
            deathLocation = reader.ReadPosition();
        }

        int portalCooldown = reader.ReadVarInt();
        int seaLevel = reader.ReadVarInt();
        bool onlineMode = reader.ReadBoolean();
        bool enforcesSecureChat = reader.ReadBoolean();

        return new LoginPlayPacket(
            entityId, isHardcore, dimensionNames, maxPlayers, viewDistance, simulationDistance,
            reducedDebugInfo, enableRespawnScreen, doLimitedCrafting, dimensionType, dimensionName,
            hashedSeed, gameMode, previousGameMode, isDebug, isFlat, hasDeathLocation,
            deathDimensionName, deathLocation, portalCooldown, seaLevel, onlineMode, enforcesSecureChat);
    }
}
