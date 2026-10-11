namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// A network-quantized Vec3 used by entity movement (26.3 LpVec3). Compresses each axis to 15
/// bits plus a scale. Zero vectors are a single 0x00 byte.
/// </summary>
public static class LpVec3IO
{
    /// <summary>Encodes the movement vector with the 26.3 low-precision quantizer.</summary>
    public static void Write(PacketWriter writer, double x, double y, double z)
    {
        x = double.IsNaN(x) ? 0.0 : Math.Clamp(x, -Math.Pow(2, 34), Math.Pow(2, 34));
        y = double.IsNaN(y) ? 0.0 : Math.Clamp(y, -Math.Pow(2, 34), Math.Pow(2, 34));
        z = double.IsNaN(z) ? 0.0 : Math.Clamp(z, -Math.Pow(2, 34), Math.Pow(2, 34));

        double chessboard = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (chessboard < 3.051944088384301E-5)
        {
            writer.WriteByte(0);
            return;
        }

        long scale = CeilLong(chessboard);
        bool isPartial = (scale & 3L) != scale;
        long markers = isPartial ? scale & 3L | 4L : scale;
        long buffer = markers | Pack(x / scale) << 3 | Pack(y / scale) << 18 | Pack(z / scale) << 33;

        writer.WriteByte((byte)buffer);
        writer.WriteByte((byte)(buffer >> 8));
        writer.WriteInt((int)(buffer >> 16));
        if (isPartial)
        {
            writer.WriteVarInt((int)(scale >> 2));
        }
    }

    private static long Pack(double value) => (long)Math.Round((value * 0.5 + 0.5) * 32766.0);

    private static long CeilLong(double value)
    {
        long ceil = (long)Math.Ceiling(value);
        return ceil == 0 ? 1 : ceil;
    }

    /// <summary>Reads a movement triple encoded with the 26.3 low-precision quantizer.</summary>
    public static void Read(ref PacketReader reader, out double x, out double y, out double z)
    {
        byte lowest = reader.ReadByte();
        if (lowest == 0)
        {
            x = 0;
            y = 0;
            z = 0;
            return;
        }

        byte middle = reader.ReadByte();
        int highest = reader.ReadInt();
        long buffer = (long)(highest & 0xFFFFFFFFL) << 16 | ((long)(middle & 0xFF) << 8) | (long)(lowest & 0xFF);

        long scale = lowest & 3L;
        if ((lowest & 4L) != 0)
        {
            scale |= (long)reader.ReadVarInt() << 2;
        }

        x = Unpack(buffer >> 3) * scale;
        y = Unpack(buffer >> 18) * scale;
        z = Unpack(buffer >> 33) * scale;
    }

    private static double Unpack(long value) => Math.Min(value & 0x7FFFL, 32766.0) * 2.0 / 32766.0 - 1.0;
}

/// <summary>
/// Add Entity (play/clientbound, packet ID 1, official name "add_entity"). Spawns an entity.
/// Field order verified from the 26.3 client (ClientboundAddEntityPacket.STREAM_CODEC) and the
/// Mojang server data generator (report protocol_id 1):
/// Entity ID (VarInt), UUID (2 longs), Type (VarInt, minecraft:entity_type registry),
/// X/Y/Z (Double), Movement (LpVec3), X Rot (Byte, packed degrees), Y Rot (Byte), Y Head Rot (Byte),
/// Data (VarInt; entity-type-specific — for item entities the item stack arrives via
/// set_entity_data instead).
/// </summary>
public sealed class AddEntityPacket : IPacket
{
    /// <summary>Entity type registry id of <c>minecraft:item</c> (26.3 server reports).</summary>
    public const int EntityTypeItem = 72;

    public AddEntityPacket(
        int entityId,
        long uuidMsb,
        long uuidLsb,
        int entityTypeId,
        double x,
        double y,
        double z,
        double movementX,
        double movementY,
        double movementZ,
        float xRot,
        float yRot,
        float yHeadRot,
        int data)
    {
        EntityId = entityId;
        UuidMsb = uuidMsb;
        UuidLsb = uuidLsb;
        EntityTypeId = entityTypeId;
        X = x;
        Y = y;
        Z = z;
        MovementX = movementX;
        MovementY = movementY;
        MovementZ = movementZ;
        XRot = xRot;
        YRot = yRot;
        YHeadRot = yHeadRot;
        Data = data;
    }

    public int EntityId { get; }

    public long UuidMsb { get; }

    public long UuidLsb { get; }

    public int EntityTypeId { get; }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    public double MovementX { get; }

    public double MovementY { get; }

    public double MovementZ { get; }

    public float XRot { get; }

    public float YRot { get; }

    public float YHeadRot { get; }

    public int Data { get; }
}

/// <summary>Codec for <see cref="AddEntityPacket"/>.</summary>
public sealed class AddEntityCodec : PacketCodec<AddEntityPacket>
{
    /// <summary>Singleton access.</summary>
    public static AddEntityCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 1;

    /// <inheritdoc />
    protected override void EncodeBody(AddEntityPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.EntityId);
        writer.WriteLong(packet.UuidMsb);
        writer.WriteLong(packet.UuidLsb);
        writer.WriteVarInt(packet.EntityTypeId);
        writer.WriteDouble(packet.X);
        writer.WriteDouble(packet.Y);
        writer.WriteDouble(packet.Z);
        LpVec3IO.Write(writer, packet.MovementX, packet.MovementY, packet.MovementZ);
        writer.WriteByte(PackDegrees(packet.XRot));
        writer.WriteByte(PackDegrees(packet.YRot));
        writer.WriteByte(PackDegrees(packet.YHeadRot));
        writer.WriteVarInt(packet.Data);
    }

    /// <inheritdoc />
    protected override AddEntityPacket DecodeBody(ref PacketReader reader)
    {
        int entityId = reader.ReadVarInt();
        long msb = reader.ReadLong();
        long lsb = reader.ReadLong();
        int entityTypeId = reader.ReadVarInt();
        double x = reader.ReadDouble();
        double y = reader.ReadDouble();
        double z = reader.ReadDouble();
        LpVec3IO.Read(ref reader, out double movementX, out double movementY, out double movementZ);
        byte xRot = reader.ReadByte();
        byte yRot = reader.ReadByte();
        byte yHeadRot = reader.ReadByte();
        int data = reader.ReadVarInt();
        return new AddEntityPacket(
            entityId, msb, lsb, entityTypeId, x, y, z,
            movementX, movementY, movementZ,
            UnpackDegrees(xRot), UnpackDegrees(yRot), UnpackDegrees(yHeadRot), data);
    }

    private static byte PackDegrees(float degrees) => (byte)Math.Round(degrees * (256.0 / 360.0));

    private static float UnpackDegrees(byte packed) => (packed & 0xFF) * (360.0f / 256.0f);
}
