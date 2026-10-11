namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Confirm Teleportation (play/serverbound, packet ID 0, official name "accept_teleportation").
/// The client confirms a Synchronize Player Position by echoing its Teleport ID and reporting
/// the resulting position/rotation.
/// Field order per docs/data/packets.json: Teleport ID (VarInt), X/Y/Z (Double), Yaw/Pitch (Float).
/// </summary>
public sealed class ConfirmTeleportationPacket : IPacket
{
    public ConfirmTeleportationPacket(int teleportId, double x, double y, double z, float yaw, float pitch)
    {
        TeleportId = teleportId;
        X = x;
        Y = y;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
    }

    public int TeleportId { get; }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public float Yaw { get; }

    public float Pitch { get; }
}

/// <summary>Codec for <see cref="ConfirmTeleportationPacket"/>.</summary>
public sealed class ConfirmTeleportationCodec : PacketCodec<ConfirmTeleportationPacket>
{
    /// <summary>Singleton access.</summary>
    public static ConfirmTeleportationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 0;

    /// <inheritdoc />
    protected override void EncodeBody(ConfirmTeleportationPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.TeleportId);
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.Y);
        writer.WriteDouble(packet.Z);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
    }

    /// <inheritdoc />
    protected override ConfirmTeleportationPacket DecodeBody(ref PacketReader reader)
        => new(
            reader.ReadVarInt(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadFloat(),
            reader.ReadFloat());
}

/// <summary>
/// Set Player Position and Rotation (play/serverbound, packet ID 31, official name
/// "move_player_pos_rot"). Field order per docs/data/packets.json: X/Y/Z (Double, feet Y),
/// Yaw/Pitch (Float), Flags (Byte bit field: 0x01 on ground, 0x02 pushing against wall).
/// </summary>
public sealed class SetPlayerPositionAndRotationPacket : IPacket
{
    public SetPlayerPositionAndRotationPacket(double x, double feetY, double z, float yaw, float pitch, byte flags)
    {
        X = x;
        FeetY = feetY;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
        Flags = flags;
    }

    public double X { get; }

    /// <summary>Absolute feet position (normally Head Y - 1.62).</summary>
    public double FeetY { get; }

    public double Z { get; }

    public float Yaw { get; }

    public float Pitch { get; }

    /// <summary>0x01 on ground, 0x02 pushing against wall.</summary>
    public byte Flags { get; }
}

/// <summary>Codec for <see cref="SetPlayerPositionAndRotationPacket"/>.</summary>
public sealed class SetPlayerPositionAndRotationCodec : PacketCodec<SetPlayerPositionAndRotationPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetPlayerPositionAndRotationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 31;

    /// <inheritdoc />
    protected override void EncodeBody(SetPlayerPositionAndRotationPacket packet, PacketWriter writer)
    {
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.FeetY);
        writer.WriteDouble(packet.Z);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
        writer.WriteByte(packet.Flags);
    }

    /// <inheritdoc />
    protected override SetPlayerPositionAndRotationPacket DecodeBody(ref PacketReader reader)
        => new(
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadFloat(),
            reader.ReadFloat(),
            reader.ReadByte());
}

/// <summary>
/// Set Player Position (play/serverbound, packet ID 30, official name "move_player_pos").
/// Field order per docs/data/packets.json and the 26.3 client (ServerboundMovePlayerPacket.Pos):
/// X/Y/Z (Double, feet Y), Flags (Byte bit field: 0x01 on ground, 0x02 pushing against wall).
/// </summary>
public sealed class SetPlayerPositionPacket : IPacket
{
    public SetPlayerPositionPacket(double x, double feetY, double z, byte flags)
    {
        X = x;
        FeetY = feetY;
        Z = z;
        Flags = flags;
    }

    public double X { get; }

    /// <summary>Absolute feet position (normally Head Y - 1.62).</summary>
    public double FeetY { get; }

    public double Z { get; }

    /// <summary>0x01 on ground, 0x02 pushing against wall.</summary>
    public byte Flags { get; }
}

/// <summary>Codec for <see cref="SetPlayerPositionPacket"/>.</summary>
public sealed class SetPlayerPositionCodec : PacketCodec<SetPlayerPositionPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetPlayerPositionCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 30;

    /// <inheritdoc />
    protected override void EncodeBody(SetPlayerPositionPacket packet, PacketWriter writer)
    {
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.FeetY);
        writer.WriteDouble(packet.Z);
        writer.WriteByte(packet.Flags);
    }

    /// <inheritdoc />
    protected override SetPlayerPositionPacket DecodeBody(ref PacketReader reader)
        => new(
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadDouble(),
            reader.ReadByte());
}

/// <summary>
/// Set Player Rotation (play/serverbound, packet ID 32, official name "move_player_rot").
/// Field order per docs/data/packets.json and the 26.3 client (ServerboundMovePlayerPacket.Rot):
/// Yaw/Pitch (Float), Flags (Byte bit field: 0x01 on ground, 0x02 pushing against wall).
/// </summary>
public sealed class SetPlayerRotationPacket : IPacket
{
    public SetPlayerRotationPacket(float yaw, float pitch, byte flags)
    {
        Yaw = yaw;
        Pitch = pitch;
        Flags = flags;
    }

    public float Yaw { get; }

    public float Pitch { get; }

    /// <summary>0x01 on ground, 0x02 pushing against wall.</summary>
    public byte Flags { get; }
}

/// <summary>Codec for <see cref="SetPlayerRotationPacket"/>.</summary>
public sealed class SetPlayerRotationCodec : PacketCodec<SetPlayerRotationPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetPlayerRotationCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 32;

    /// <inheritdoc />
    protected override void EncodeBody(SetPlayerRotationPacket packet, PacketWriter writer)
    {
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
        writer.WriteByte(packet.Flags);
    }

    /// <inheritdoc />
    protected override SetPlayerRotationPacket DecodeBody(ref PacketReader reader)
        => new(
            reader.ReadFloat(),
            reader.ReadFloat(),
            reader.ReadByte());
}

/// <summary>
/// Set Player Movement Flags (play/serverbound, packet ID 33, official name
/// "move_player_status_only"). Field order per docs/data/packets.json and the 26.3 client
/// (ServerboundMovePlayerPacket.StatusOnly): Flags (Byte bit field: 0x01 on ground,
/// 0x02 pushing against wall).
/// </summary>
public sealed class SetPlayerStatusOnlyPacket : IPacket
{
    public SetPlayerStatusOnlyPacket(byte flags)
    {
        Flags = flags;
    }

    /// <summary>0x01 on ground, 0x02 pushing against wall.</summary>
    public byte Flags { get; }
}

/// <summary>Codec for <see cref="SetPlayerStatusOnlyPacket"/>.</summary>
public sealed class SetPlayerStatusOnlyCodec : PacketCodec<SetPlayerStatusOnlyPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetPlayerStatusOnlyCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 33;

    /// <inheritdoc />
    protected override void EncodeBody(SetPlayerStatusOnlyPacket packet, PacketWriter writer)
        => writer.WriteByte(packet.Flags);

    /// <inheritdoc />
    protected override SetPlayerStatusOnlyPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadByte());
}

/// <summary>
/// Punch (play/serverbound, packet ID 46, official name "punch"). Sent when the player left
/// clicks; in 26.3 this replaced swing_arm. No fields (docs/data/packets.json; the 26.3
/// client's ServerboundPunchPacket has no payload).
/// </summary>
public sealed class PunchPacket : IPacket
{
}

/// <summary>Codec for <see cref="PunchPacket"/>.</summary>
public sealed class PunchCodec : PacketCodec<PunchPacket>
{
    /// <summary>Singleton access.</summary>
    public static PunchCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 46;

    /// <inheritdoc />
    protected override void EncodeBody(PunchPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override PunchPacket DecodeBody(ref PacketReader reader) => new();
}

/// <summary>
/// Player Command (play/serverbound, packet ID 42, official name "player_command").
/// Field order per docs/data/packets.json and the 26.3 client (ServerboundPlayerCommandPacket):
/// Entity ID (VarInt), Action ID (VarInt Enum), Jump Boost (VarInt).
/// </summary>
public sealed class PlayerCommandPacket : IPacket
{
    /// <summary>Valid action range from the 26.3 client enum (0..6).</summary>
    public const int MinActionId = 0;

    /// <summary>Valid action range from the 26.3 client enum (0..6).</summary>
    public const int MaxActionId = 6;

    public PlayerCommandPacket(int entityId, int actionId, int jumpBoost)
    {
        EntityId = entityId;
        ActionId = actionId;
        JumpBoost = jumpBoost;
    }

    /// <summary>The player's entity ID (ignored by the vanilla server).</summary>
    public int EntityId { get; }

    /// <summary>The ID of the action (see the constants/scoped enum in docs/data/packets.json).</summary>
    public int ActionId { get; }

    /// <summary>Only used by "start jump with horse"; 0 otherwise.</summary>
    public int JumpBoost { get; }
}

/// <summary>Codec for <see cref="PlayerCommandPacket"/>.</summary>
public sealed class PlayerCommandCodec : PacketCodec<PlayerCommandPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayerCommandCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 42;

    /// <inheritdoc />
    protected override void EncodeBody(PlayerCommandPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.EntityId);
        writer.WriteVarInt(packet.ActionId);
        writer.WriteVarInt(packet.JumpBoost);
    }

    /// <inheritdoc />
    protected override PlayerCommandPacket DecodeBody(ref PacketReader reader)
    {
        int entityId = reader.ReadVarInt();
        int actionId = reader.ReadVarInt();
        int jumpBoost = reader.ReadVarInt();

        if (actionId < PlayerCommandPacket.MinActionId || actionId > PlayerCommandPacket.MaxActionId)
        {
            throw new InvalidDataException($"Player Command action {actionId} is out of range.");
        }

        return new PlayerCommandPacket(entityId, actionId, jumpBoost);
    }
}

/// <summary>
/// Set Held Item (serverbound) (play/serverbound, packet ID 54, official name
/// "set_carried_item"). Field order per docs/data/packets.json and the 26.3 client
/// (ServerboundSetCarriedItemPacket): Slot (Short, 0-8).
/// </summary>
public sealed class SetCarriedItemPacket : IPacket
{
    public SetCarriedItemPacket(short slot)
    {
        Slot = slot;
    }

    /// <summary>The slot the player has selected (0-8).</summary>
    public short Slot { get; }
}

/// <summary>Codec for <see cref="SetCarriedItemPacket"/>.</summary>
public sealed class SetCarriedItemCodec : PacketCodec<SetCarriedItemPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetCarriedItemCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 54;

    /// <inheritdoc />
    protected override void EncodeBody(SetCarriedItemPacket packet, PacketWriter writer)
        => writer.WriteShort(packet.Slot);

    /// <inheritdoc />
    protected override SetCarriedItemPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadShort());
}

/// <summary>
/// Player Action (play/serverbound, packet ID 41, official name "player_action").
/// Field order per docs/data/packets.json and the 26.3 client (ServerboundPlayerActionPacket):
/// Status (VarInt Enum), Location (Position), Face (Byte Enum), Sequence (VarInt).
/// </summary>
public sealed class PlayerActionPacket : IPacket
{
    /// <summary>Valid status range from the 26.3 client enum (0..8).</summary>
    public const int MinStatusId = 0;

    /// <summary>Valid status range from the 26.3 client enum (0..8).</summary>
    public const int MaxStatusId = 8;

    /// <summary>Started destroying the block at Location (client enum START_DESTROY_BLOCK).</summary>
    public const int StartDestroyBlock = 0;

    /// <summary>The player changed the face being dug (CHANGE_DESTROY_DIRECTION).</summary>
    public const int ChangeDestroyDirection = 1;

    /// <summary>Canceled/did not complete digging (ABORT_DESTROY_BLOCK).</summary>
    public const int AbortDestroyBlock = 2;

    /// <summary>Finished digging; the block should be broken (STOP_DESTROY_BLOCK).</summary>
    public const int StopDestroyBlock = 3;

    /// <summary>Dropped the entire held stack (DROP_ALL_ITEMS).</summary>
    public const int DropAllItems = 4;

    /// <summary>Dropped one item (DROP_ITEM).</summary>
    public const int DropItem = 5;

    /// <summary>Released the use-item button (RELEASE_USE_ITEM).</summary>
    public const int ReleaseUseItem = 6;

    /// <summary>Swapped the held item to the offhand (SWAP_ITEM_WITH_OFFHAND).</summary>
    public const int SwapItemWithOffhand = 7;

    /// <summary>Lunge/attack in the air (STAB).</summary>
    public const int Stab = 8;

    /// <summary>Valid face range (0..5, one per axis direction).</summary>
    public const int MinFace = 0;

    /// <summary>Valid face range (0..5, one per axis direction).</summary>
    public const int MaxFace = 5;

    public PlayerActionPacket(int status, BlockPosition location, int face, int sequence)
    {
        Status = status;
        Location = location;
        Face = face;
        Sequence = sequence;
    }

    /// <summary>The action the player is taking (see docs/data/packets.json "Player Action").</summary>
    public int Status { get; }

    /// <summary>The block position.</summary>
    public BlockPosition Location { get; }

    /// <summary>The face being hit (0 bottom .. 5 north).</summary>
    public int Face { get; }

    /// <summary>Block change sequence number.</summary>
    public int Sequence { get; }
}

/// <summary>Codec for <see cref="PlayerActionPacket"/>.</summary>
public sealed class PlayerActionCodec : PacketCodec<PlayerActionPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayerActionCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 41;

    /// <inheritdoc />
    protected override void EncodeBody(PlayerActionPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.Status);
        writer.WritePosition(packet.Location);
        writer.WriteByte((byte)packet.Face);
        writer.WriteVarInt(packet.Sequence);
    }

    /// <inheritdoc />
    protected override PlayerActionPacket DecodeBody(ref PacketReader reader)
    {
        int status = reader.ReadVarInt();
        BlockPosition location = reader.ReadPosition();
        int face = reader.ReadByte();
        int sequence = reader.ReadVarInt();

        if (status < PlayerActionPacket.MinStatusId || status > PlayerActionPacket.MaxStatusId)
        {
            throw new InvalidDataException($"Player Action status {status} is out of range.");
        }

        if (face < PlayerActionPacket.MinFace || face > PlayerActionPacket.MaxFace)
        {
            throw new InvalidDataException($"Player Action face {face} is out of range.");
        }

        return new PlayerActionPacket(status, location, face, sequence);
    }
}

/// <summary>
/// Use Item On (play/serverbound, packet ID 66, official name "use_item_on").
/// Field order per docs/data/packets.json and the 26.3 client (ServerboundUseItemOnPacket,
/// BlockHitResult.STREAM_CODEC): Hand (VarInt Enum), Location (Position), Face (VarInt Enum),
/// Cursor Position X/Y/Z (Float), Inside block (Boolean), World Border Hit (Boolean),
/// Sequence (VarInt).
/// </summary>
public sealed class UseItemOnPacket : IPacket
{
    /// <summary>0 = main hand, 1 = off hand.</summary>
    public const int MinHand = 0;

    /// <summary>0 = main hand, 1 = off hand.</summary>
    public const int MaxHand = 1;

    public UseItemOnPacket(
        int hand,
        BlockPosition location,
        int face,
        float cursorX,
        float cursorY,
        float cursorZ,
        bool insideBlock,
        bool worldBorderHit,
        int sequence)
    {
        Hand = hand;
        Location = location;
        Face = face;
        CursorX = cursorX;
        CursorY = cursorY;
        CursorZ = cursorZ;
        InsideBlock = insideBlock;
        WorldBorderHit = worldBorderHit;
        Sequence = sequence;
    }

    /// <summary>The hand: 0 main, 1 off.</summary>
    public int Hand { get; }

    /// <summary>The block position.</summary>
    public BlockPosition Location { get; }

    /// <summary>The face on which the block is placed (0..5).</summary>
    public int Face { get; }

    public float CursorX { get; }

    public float CursorY { get; }

    public float CursorZ { get; }

    /// <summary>True when the player's head is inside a block.</summary>
    public bool InsideBlock { get; }

    /// <summary>Seems to always be false.</summary>
    public bool WorldBorderHit { get; }

    /// <summary>Block change sequence number.</summary>
    public int Sequence { get; }
}

/// <summary>Codec for <see cref="UseItemOnPacket"/>.</summary>
public sealed class UseItemOnCodec : PacketCodec<UseItemOnPacket>
{
    /// <summary>Singleton access.</summary>
    public static UseItemOnCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 66;

    /// <inheritdoc />
    protected override void EncodeBody(UseItemOnPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.Hand);
        writer.WritePosition(packet.Location);
        writer.WriteVarInt(packet.Face);
        writer.WriteFloat(packet.CursorX);
        writer.WriteFloat(packet.CursorY);
        writer.WriteFloat(packet.CursorZ);
        writer.WriteBoolean(packet.InsideBlock);
        writer.WriteBoolean(packet.WorldBorderHit);
        writer.WriteVarInt(packet.Sequence);
    }

    /// <inheritdoc />
    protected override UseItemOnPacket DecodeBody(ref PacketReader reader)
    {
        int hand = reader.ReadVarInt();
        BlockPosition location = reader.ReadPosition();
        int face = reader.ReadVarInt();
        float cursorX = reader.ReadFloat();
        float cursorY = reader.ReadFloat();
        float cursorZ = reader.ReadFloat();
        bool insideBlock = reader.ReadBoolean();
        bool worldBorderHit = reader.ReadBoolean();
        int sequence = reader.ReadVarInt();

        if (hand < UseItemOnPacket.MinHand || hand > UseItemOnPacket.MaxHand)
        {
            throw new InvalidDataException($"Use Item On hand {hand} is out of range.");
        }

        if (face < PlayerActionPacket.MinFace || face > PlayerActionPacket.MaxFace)
        {
            throw new InvalidDataException($"Use Item On face {face} is out of range.");
        }

        return new UseItemOnPacket(hand, location, face, cursorX, cursorY, cursorZ, insideBlock, worldBorderHit, sequence);
    }
}

/// <summary>
/// Use Item (play/serverbound, packet ID 67, official name "use_item").
/// Field order per docs/data/packets.json and the 26.3 client (ServerboundUseItemPacket):
/// Hand (VarInt Enum), Sequence (VarInt), Yaw (Float), Pitch (Float).
/// </summary>
public sealed class UseItemPacket : IPacket
{
    public UseItemPacket(int hand, int sequence, float yaw, float pitch)
    {
        Hand = hand;
        Sequence = sequence;
        Yaw = yaw;
        Pitch = pitch;
    }

    /// <summary>The hand: 0 main, 1 off.</summary>
    public int Hand { get; }

    /// <summary>Block change sequence number.</summary>
    public int Sequence { get; }

    /// <summary>Player head rotation around the Y axis.</summary>
    public float Yaw { get; }

    /// <summary>Player head rotation around the X axis.</summary>
    public float Pitch { get; }
}

/// <summary>Codec for <see cref="UseItemPacket"/>.</summary>
public sealed class UseItemCodec : PacketCodec<UseItemPacket>
{
    /// <summary>Singleton access.</summary>
    public static UseItemCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 67;

    /// <inheritdoc />
    protected override void EncodeBody(UseItemPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.Hand);
        writer.WriteVarInt(packet.Sequence);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
    }

    /// <inheritdoc />
    protected override UseItemPacket DecodeBody(ref PacketReader reader)
    {
        int hand = reader.ReadVarInt();
        int sequence = reader.ReadVarInt();
        float yaw = reader.ReadFloat();
        float pitch = reader.ReadFloat();

        if (hand < UseItemOnPacket.MinHand || hand > UseItemOnPacket.MaxHand)
        {
            throw new InvalidDataException($"Use Item hand {hand} is out of range.");
        }

        return new UseItemPacket(hand, sequence, yaw, pitch);
    }
}

/// <summary>
/// Attack (play/serverbound, packet ID 1, official name "attack"). Sent when attacking an
/// entity. Field: Entity ID (VarInt) — the 26.3 client's ServerboundAttackPacket.
/// </summary>
public sealed class AttackPacket : IPacket
{
    public AttackPacket(int entityId)
    {
        EntityId = entityId;
    }

    /// <summary>The attacked entity's ID.</summary>
    public int EntityId { get; }
}

/// <summary>Codec for <see cref="AttackPacket"/>.</summary>
public sealed class AttackCodec : PacketCodec<AttackPacket>
{
    /// <summary>Singleton access.</summary>
    public static AttackCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 1;

    /// <inheritdoc />
    protected override void EncodeBody(AttackPacket packet, PacketWriter writer)
        => writer.WriteVarInt(packet.EntityId);

    /// <inheritdoc />
    protected override AttackPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt());
}

/// <summary>A hashed data-component entry inside a <see cref="HashedStackData"/>.</summary>
public sealed record HashedComponentHash(int TypeId, int Hash);

/// <summary>
/// A hashed item stack as sent by the 26.3 client (<c>HashedStack</c>): a presence flag
/// followed, when present, by the item's global ID (VarInt), count (VarInt), a map of added
/// component hashes (each VarInt type ID + Int hash) and a set of removed component type IDs
/// (each VarInt).
/// </summary>
public sealed record HashedStackData(bool Present, int ItemId, int Count, HashedComponentHash[] Added, int[] Removed)
{
    /// <summary>The absent (empty) stack.</summary>
    public static readonly HashedStackData Empty = new(false, 0, 0, [], []);
}

/// <summary>A changed container slot: slot number and the hashed stack the client expects there.</summary>
public sealed record SlotHashEntry(short Slot, HashedStackData Stack);

/// <summary>
/// Click Container (play/serverbound, packet ID 18, official name "container_click"). Wire
/// format from the 26.3 client (ServerboundContainerClickPacket): Container ID (VarInt),
/// State ID (VarInt), Slot (Short), Button (Byte), Input (VarInt enum 0-6), Changed Slots
/// (map of Short -> HashedStack, up to 128 entries), Carried Item (HashedStack).
/// </summary>
public sealed class ContainerClickPacket : IPacket
{
    /// <summary>Maximum changed-slot entries (client constant).</summary>
    public const int MaxChangedSlots = 128;

    /// <summary>Maximum component entries in a hashed stack (client constant).</summary>
    public const int MaxComponentEntries = 256;

    public ContainerClickPacket(
        int containerId,
        int stateId,
        short slotNum,
        byte buttonNum,
        int inputId,
        SlotHashEntry[] changedSlots,
        HashedStackData carriedItem)
    {
        ContainerId = containerId;
        StateId = stateId;
        SlotNum = slotNum;
        ButtonNum = buttonNum;
        InputId = inputId;
        ChangedSlots = changedSlots;
        CarriedItem = carriedItem;
    }

    public int ContainerId { get; }

    public int StateId { get; }

    public short SlotNum { get; }

    public byte ButtonNum { get; }

    /// <summary>The click input: 0 pickup, 1 quick-move, 2 swap, 3 clone, 4 throw, 5 quick-craft, 6 pickup-all.</summary>
    public int InputId { get; }

    public SlotHashEntry[] ChangedSlots { get; }

    public HashedStackData CarriedItem { get; }
}

/// <summary>Codec for <see cref="ContainerClickPacket"/>.</summary>
public sealed class ContainerClickCodec : PacketCodec<ContainerClickPacket>
{
    /// <summary>Singleton access.</summary>
    public static ContainerClickCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 18;

    /// <inheritdoc />
    protected override void EncodeBody(ContainerClickPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.ContainerId);
        writer.WriteVarInt(packet.StateId);
        writer.WriteShort(packet.SlotNum);
        writer.WriteByte(packet.ButtonNum);
        writer.WriteVarInt(packet.InputId);

        writer.WriteVarInt(packet.ChangedSlots.Length);
        foreach (SlotHashEntry entry in packet.ChangedSlots)
        {
            writer.WriteShort(entry.Slot);
            WriteHashedStack(writer, entry.Stack);
        }

        WriteHashedStack(writer, packet.CarriedItem);
    }

    /// <inheritdoc />
    protected override ContainerClickPacket DecodeBody(ref PacketReader reader)
    {
        int containerId = reader.ReadVarInt();
        if (containerId < 0)
        {
            throw new InvalidDataException($"Negative container ID ({containerId}).");
        }

        int stateId = reader.ReadVarInt();
        short slotNum = reader.ReadShort();
        byte buttonNum = reader.ReadByte();
        int inputId = reader.ReadVarInt();
        if (inputId < 0 || inputId > 6)
        {
            throw new InvalidDataException($"Container Click input {inputId} is out of range.");
        }

        int changedSlotsCount = reader.ReadVarInt();
        if (changedSlotsCount < 0 || changedSlotsCount > ContainerClickPacket.MaxChangedSlots)
        {
            throw new InvalidDataException(
                $"Container Click has {changedSlotsCount} changed slots (limit {ContainerClickPacket.MaxChangedSlots}).");
        }

        var changedSlots = new SlotHashEntry[changedSlotsCount];
        for (int i = 0; i < changedSlotsCount; i++)
        {
            short slot = reader.ReadShort();
            changedSlots[i] = new SlotHashEntry(slot, ReadHashedStack(ref reader));
        }

        HashedStackData carriedItem = ReadHashedStack(ref reader);

        return new ContainerClickPacket(containerId, stateId, slotNum, buttonNum, inputId, changedSlots, carriedItem);
    }

    private static void WriteHashedStack(PacketWriter writer, HashedStackData stack)
    {
        writer.WriteBoolean(stack.Present);
        if (!stack.Present)
        {
            return;
        }

        writer.WriteVarInt(stack.ItemId);
        writer.WriteVarInt(stack.Count);

        writer.WriteVarInt(stack.Added.Length);
        foreach (HashedComponentHash component in stack.Added)
        {
            writer.WriteVarInt(component.TypeId);
            writer.WriteInt(component.Hash);
        }

        writer.WriteVarInt(stack.Removed.Length);
        foreach (int componentTypeId in stack.Removed)
        {
            writer.WriteVarInt(componentTypeId);
        }
    }

    private static HashedStackData ReadHashedStack(ref PacketReader reader)
    {
        if (!reader.ReadBoolean())
        {
            return HashedStackData.Empty;
        }

        int itemId = reader.ReadVarInt();
        int count = reader.ReadVarInt();

        int addedCount = reader.ReadVarInt();
        if (addedCount < 0 || addedCount > ContainerClickPacket.MaxComponentEntries)
        {
            throw new InvalidDataException(
                $"Hashed stack has {addedCount} added components (limit {ContainerClickPacket.MaxComponentEntries}).");
        }

        var added = new HashedComponentHash[addedCount];
        for (int i = 0; i < addedCount; i++)
        {
            added[i] = new HashedComponentHash(reader.ReadVarInt(), reader.ReadInt());
        }

        int removedCount = reader.ReadVarInt();
        if (removedCount < 0 || removedCount > ContainerClickPacket.MaxComponentEntries)
        {
            throw new InvalidDataException(
                $"Hashed stack has {removedCount} removed components (limit {ContainerClickPacket.MaxComponentEntries}).");
        }

        var removed = new int[removedCount];
        for (int i = 0; i < removedCount; i++)
        {
            removed[i] = reader.ReadVarInt();
        }

        return new HashedStackData(true, itemId, count, added, removed);
    }
}

/// <summary>
/// Close Container (play/serverbound, packet ID 19, official name "container_close").
/// Field: Container ID (VarInt) — 26.3 client ServerboundContainerClosePacket.
/// </summary>
public sealed class ContainerClosePacket : IPacket
{
    public ContainerClosePacket(int containerId)
    {
        ContainerId = containerId;
    }

    public int ContainerId { get; }
}

/// <summary>Codec for <see cref="ContainerClosePacket"/>.</summary>
public sealed class ContainerCloseCodec : PacketCodec<ContainerClosePacket>
{
    /// <summary>Singleton access.</summary>
    public static ContainerCloseCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 19;

    /// <inheritdoc />
    protected override void EncodeBody(ContainerClosePacket packet, PacketWriter writer)
        => writer.WriteVarInt(packet.ContainerId);

    /// <inheritdoc />
    protected override ContainerClosePacket DecodeBody(ref PacketReader reader)
    {
        int containerId = reader.ReadVarInt();
        if (containerId < 0)
        {
            throw new InvalidDataException($"Negative container ID ({containerId}).");
        }

        return new ContainerClosePacket(containerId);
    }
}

/// <summary>
/// Move Vehicle (play/serverbound, packet ID 34, official name "move_vehicle").
/// Field order from the 26.3 client (ServerboundMoveVehiclePacket / PositionAndRotation):
/// X/Y/Z (Double), Yaw/Pitch (Float), On Ground (Boolean).
/// </summary>
public sealed class MoveVehiclePacket : IPacket
{
    public MoveVehiclePacket(double x, double y, double z, float yaw, float pitch, bool onGround)
    {
        X = x;
        Y = y;
        Z = z;
        Yaw = yaw;
        Pitch = pitch;
        OnGround = onGround;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public float Yaw { get; }

    public float Pitch { get; }

    public bool OnGround { get; }
}

/// <summary>Codec for <see cref="MoveVehiclePacket"/>.</summary>
public sealed class MoveVehicleCodec : PacketCodec<MoveVehiclePacket>
{
    /// <summary>Singleton access.</summary>
    public static MoveVehicleCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 34;

    /// <inheritdoc />
    protected override void EncodeBody(MoveVehiclePacket packet, PacketWriter writer)
    {
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.Y);
        writer.WriteDouble(packet.Z);
        writer.WriteFloat(packet.Yaw);
        writer.WriteFloat(packet.Pitch);
        writer.WriteBoolean(packet.OnGround);
    }

    /// <inheritdoc />
    protected override MoveVehiclePacket DecodeBody(ref PacketReader reader)
    {
        double x = reader.ReadDouble();
        double y = reader.ReadDouble();
        double z = reader.ReadDouble();
        float yaw = reader.ReadFloat();
        float pitch = reader.ReadFloat();
        bool onGround = reader.ReadBoolean();

        if (double.IsNaN(x) || double.IsInfinity(x)
            || double.IsNaN(y) || double.IsInfinity(y)
            || double.IsNaN(z) || double.IsInfinity(z))
        {
            throw new InvalidDataException("Vehicle position contains a non-finite value.");
        }

        return new MoveVehiclePacket(x, y, z, yaw, pitch, onGround);
    }
}

/// <summary>
/// Player Abilities (serverbound) (play/serverbound, packet ID 40, official name
/// "player_abilities"). Field: Flags (Byte, 0x02 = flying) — 26.3 client
/// ServerboundPlayerAbilitiesPacket.
/// </summary>
public sealed class ServerPlayerAbilitiesPacket : IPacket
{
    public ServerPlayerAbilitiesPacket(byte flags)
    {
        Flags = flags;
    }

    /// <summary>Bit mask; 0x02 means the player is flying.</summary>
    public byte Flags { get; }
}

/// <summary>Codec for <see cref="ServerPlayerAbilitiesPacket"/>.</summary>
public sealed class ServerPlayerAbilitiesCodec : PacketCodec<ServerPlayerAbilitiesPacket>
{
    /// <summary>Singleton access.</summary>
    public static ServerPlayerAbilitiesCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 40;

    /// <inheritdoc />
    protected override void EncodeBody(ServerPlayerAbilitiesPacket packet, PacketWriter writer)
        => writer.WriteByte(packet.Flags);

    /// <inheritdoc />
    protected override ServerPlayerAbilitiesPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadByte());
}

/// <summary>
/// Player Input (play/serverbound, packet ID 43, official name "player_input"). Field: Flags
/// (Unsigned Byte) with bits forward=1, backward=2, left=4, right=8, jump=16, shift=32,
/// sprint=64 (26.3 client Input.STREAM_CODEC).
/// </summary>
public sealed class PlayerInputPacket : IPacket
{
    public PlayerInputPacket(byte flags)
    {
        Flags = flags;
    }

    /// <summary>Bit mask of movement inputs (see type doc).</summary>
    public byte Flags { get; }
}

/// <summary>Codec for <see cref="PlayerInputPacket"/>.</summary>
public sealed class PlayerInputCodec : PacketCodec<PlayerInputPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayerInputCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 43;

    /// <inheritdoc />
    protected override void EncodeBody(PlayerInputPacket packet, PacketWriter writer)
        => writer.WriteByte(packet.Flags);

    /// <inheritdoc />
    protected override PlayerInputPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadByte());
}

/// <summary>
/// Change Recipe Book Settings (play/serverbound, packet ID 47, official name
/// "recipe_book_change_settings"). Field order from the 26.3 client
/// (ServerboundRecipeBookChangeSettingsPacket): Book Type (VarInt enum 0-3), Book Open
/// (Boolean), Filter Active (Boolean).
/// </summary>
public sealed class RecipeBookChangeSettingsPacket : IPacket
{
    /// <summary>Valid book range (0 crafting, 1 furnace, 2 blast furnace, 3 smoker).</summary>
    public const int MinBookType = 0;

    /// <summary>Valid book range (0 crafting, 1 furnace, 2 blast furnace, 3 smoker).</summary>
    public const int MaxBookType = 3;

    public RecipeBookChangeSettingsPacket(int bookType, bool isOpen, bool isFiltering)
    {
        BookType = bookType;
        IsOpen = isOpen;
        IsFiltering = isFiltering;
    }

    public int BookType { get; }

    public bool IsOpen { get; }

    public bool IsFiltering { get; }
}

/// <summary>Codec for <see cref="RecipeBookChangeSettingsPacket"/>.</summary>
public sealed class RecipeBookChangeSettingsCodec : PacketCodec<RecipeBookChangeSettingsPacket>
{
    /// <summary>Singleton access.</summary>
    public static RecipeBookChangeSettingsCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 47;

    /// <inheritdoc />
    protected override void EncodeBody(RecipeBookChangeSettingsPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.BookType);
        writer.WriteBoolean(packet.IsOpen);
        writer.WriteBoolean(packet.IsFiltering);
    }

    /// <inheritdoc />
    protected override RecipeBookChangeSettingsPacket DecodeBody(ref PacketReader reader)
    {
        int bookType = reader.ReadVarInt();
        bool isOpen = reader.ReadBoolean();
        bool isFiltering = reader.ReadBoolean();

        if (bookType < RecipeBookChangeSettingsPacket.MinBookType || bookType > RecipeBookChangeSettingsPacket.MaxBookType)
        {
            throw new InvalidDataException($"Recipe book type {bookType} is out of range.");
        }

        return new RecipeBookChangeSettingsPacket(bookType, isOpen, isFiltering);
    }
}

/// <summary>
/// Client Status (play/serverbound, packet ID 12, official name "client_command").
/// Field: Action (VarInt enum: 0 PERFORM_RESPAWN, 1 REQUEST_STATS, 2 REQUEST_GAMERULE_VALUES)
/// — 26.3 client ServerboundClientCommandPacket.
/// </summary>
public sealed class ClientCommandPacket : IPacket
{
    public ClientCommandPacket(int actionId)
    {
        ActionId = actionId;
    }

    /// <summary>The requested action (see type doc).</summary>
    public int ActionId { get; }
}

/// <summary>Codec for <see cref="ClientCommandPacket"/>.</summary>
public sealed class ClientCommandCodec : PacketCodec<ClientCommandPacket>
{
    /// <summary>Singleton access.</summary>
    public static ClientCommandCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 12;

    /// <inheritdoc />
    protected override void EncodeBody(ClientCommandPacket packet, PacketWriter writer)
        => writer.WriteVarInt(packet.ActionId);

    /// <inheritdoc />
    protected override ClientCommandPacket DecodeBody(ref PacketReader reader)
    {
        int actionId = reader.ReadVarInt();
        if (actionId < 0 || actionId > 2)
        {
            throw new InvalidDataException($"Client Command action {actionId} is out of range.");
        }

        return new ClientCommandPacket(actionId);
    }
}

/// <summary>
/// Chunk Batch Received (play/serverbound, packet ID 11, official name "chunk_batch_received").
/// The client sends this after receiving a Chunk Batch Started/Finished pair.
/// Field: Chunks per tick (Float).
/// </summary>
public sealed class ChunkBatchReceivedPacket : IPacket
{
    public ChunkBatchReceivedPacket(float chunksPerTick)
    {
        ChunksPerTick = chunksPerTick;
    }

    /// <summary>Desired chunks per tick.</summary>
    public float ChunksPerTick { get; }
}

/// <summary>Codec for <see cref="ChunkBatchReceivedPacket"/>.</summary>
public sealed class ChunkBatchReceivedCodec : PacketCodec<ChunkBatchReceivedPacket>
{
    /// <summary>Singleton access.</summary>
    public static ChunkBatchReceivedCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 11;

    /// <inheritdoc />
    protected override void EncodeBody(ChunkBatchReceivedPacket packet, PacketWriter writer)
        => writer.WriteFloat(packet.ChunksPerTick);

    /// <inheritdoc />
    protected override ChunkBatchReceivedPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadFloat());
}

/// <summary>
/// Player Loaded (play/serverbound, packet ID 44, official name "player_loaded").
/// Sent once the client considers itself fully loaded (first chunk batch received).
/// No fields.
/// </summary>
public sealed class PlayerLoadedPacket : IPacket
{
}

/// <summary>Codec for <see cref="PlayerLoadedPacket"/>.</summary>
public sealed class PlayerLoadedCodec : PacketCodec<PlayerLoadedPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayerLoadedCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 44;

    /// <inheritdoc />
    protected override void EncodeBody(PlayerLoadedPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override PlayerLoadedPacket DecodeBody(ref PacketReader reader) => new();
}

/// <summary>
/// Client Tick End (play/serverbound, packet ID 13, official name "client_tick_end").
/// Sent at the end of every client tick. No fields.
/// </summary>
public sealed class ClientTickEndPacket : IPacket
{
}

/// <summary>Codec for <see cref="ClientTickEndPacket"/>.</summary>
public sealed class ClientTickEndCodec : PacketCodec<ClientTickEndPacket>
{
    /// <summary>Singleton access.</summary>
    public static ClientTickEndCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 13;

    /// <inheritdoc />
    protected override void EncodeBody(ClientTickEndPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override ClientTickEndPacket DecodeBody(ref PacketReader reader) => new();
}

/// <summary>
/// Pong (play/serverbound, packet ID 45, official name "pong").
/// Client response to the clientbound Ping packet. Field: ID (Int).
/// </summary>
public sealed class PlayPongPacket : IPacket
{
    public PlayPongPacket(int id)
    {
        Id = id;
    }

    public int Id { get; }
}

/// <summary>Codec for <see cref="PlayPongPacket"/>.</summary>
public sealed class PlayPongCodec : PacketCodec<PlayPongPacket>
{
    /// <summary>Singleton access.</summary>
    public static PlayPongCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Serverbound;

    /// <inheritdoc />
    public override int PacketId => 45;

    /// <inheritdoc />
    protected override void EncodeBody(PlayPongPacket packet, PacketWriter writer)
        => writer.WriteInt(packet.Id);

    /// <inheritdoc />
    protected override PlayPongPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadInt());
}
