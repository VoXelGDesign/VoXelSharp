using VoXelSharp.Protocol.Nbt;

namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Chunk Data and Update Light (play/clientbound, packet ID 46, official name
/// "level_chunk_with_light"). Sends a chunk's terrain, lighting and block entities.
/// Field order per docs/data/packets.json and docs/data/chunk_format.md:
/// Chunk X (Int), Chunk Z (Int), Heightmaps (Prefixed Array of Heightmap),
/// Data (Prefixed Array of Byte, the encoded section array), Block Entities
/// (Prefixed Array), Light (Light Data). Verified as protocol_id 46 against the
/// Mojang data-generator report.
/// </summary>
public sealed class ChunkDataPacket : IPacket
{
    public ChunkDataPacket(
        int chunkX,
        int chunkZ,
        Heightmap[] heightmaps,
        byte[] data,
        ChunkBlockEntity[] blockEntities,
        LightData light)
    {
        ArgumentNullException.ThrowIfNull(heightmaps);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(blockEntities);
        ArgumentNullException.ThrowIfNull(light);

        if (data.Length > ChunkDataIO.MaxChunkPayload)
        {
            throw new ArgumentException(
                $"Chunk data of {data.Length} bytes exceeds the {ChunkDataIO.MaxChunkPayload}-byte limit.",
                nameof(data));
        }

        ChunkX = chunkX;
        ChunkZ = chunkZ;
        Heightmaps = heightmaps;
        Data = data;
        BlockEntities = blockEntities;
        Light = light;
    }

    public int ChunkX { get; }

    public int ChunkZ { get; }

    public Heightmap[] Heightmaps { get; }

    /// <summary>The encoded chunk-section array (from <see cref="ChunkDataIO.EncodeSections"/>).</summary>
    public byte[] Data { get; }

    public ChunkBlockEntity[] BlockEntities { get; }

    public LightData Light { get; }
}

/// <summary>Codec for <see cref="ChunkDataPacket"/>.</summary>
public sealed class ChunkDataCodec : PacketCodec<ChunkDataPacket>
{
    /// <summary>Singleton access.</summary>
    public static ChunkDataCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 46;

    /// <inheritdoc />
    protected override void EncodeBody(ChunkDataPacket packet, PacketWriter writer)
    {
        writer.WriteInt(packet.ChunkX);
        writer.WriteInt(packet.ChunkZ);

        writer.WriteVarInt(packet.Heightmaps.Length);
        foreach (Heightmap heightmap in packet.Heightmaps)
        {
            writer.WriteVarInt(heightmap.Type);
            writer.WriteLongArray(heightmap.Data);
        }

        writer.WritePrefixedByteArray(packet.Data);

        writer.WriteVarInt(packet.BlockEntities.Length);
        foreach (ChunkBlockEntity blockEntity in packet.BlockEntities)
        {
            writer.WriteByte(blockEntity.PackedXz);
            writer.WriteShort(blockEntity.Y);
            writer.WriteVarInt(blockEntity.Type);
            NbtWriter.WriteRoot(writer, blockEntity.Data);
        }

        WriteLightData(writer, packet.Light);
    }

    /// <inheritdoc />
    protected override ChunkDataPacket DecodeBody(ref PacketReader reader)
    {
        int chunkX = reader.ReadInt();
        int chunkZ = reader.ReadInt();

        int heightmapCount = reader.ReadVarInt();
        if (heightmapCount < 0)
        {
            throw new InvalidDataException($"Negative heightmap count ({heightmapCount}).");
        }

        var heightmaps = new Heightmap[heightmapCount];
        for (int i = 0; i < heightmapCount; i++)
        {
            int type = reader.ReadVarInt();
            long[] data = reader.ReadLongArray();
            heightmaps[i] = new Heightmap(type, data);
        }

        byte[] sectionData = reader.ReadPrefixedByteArray();

        int blockEntityCount = reader.ReadVarInt();
        if (blockEntityCount < 0)
        {
            throw new InvalidDataException($"Negative block entity count ({blockEntityCount}).");
        }

        var blockEntities = new ChunkBlockEntity[blockEntityCount];
        for (int i = 0; i < blockEntityCount; i++)
        {
            byte packedXz = reader.ReadByte();
            short y = reader.ReadShort();
            int type = reader.ReadVarInt();
            NbtCompound nbt = NbtReader.ReadRoot(ref reader);
            blockEntities[i] = new ChunkBlockEntity(packedXz, y, type, nbt);
        }

        LightData light = ReadLightData(ref reader);

        return new ChunkDataPacket(chunkX, chunkZ, heightmaps, sectionData, blockEntities, light);
    }

    private static void WriteLightData(PacketWriter writer, LightData light)
    {
        writer.WriteBitSet(light.SkyLightMask);
        writer.WriteBitSet(light.BlockLightMask);
        writer.WriteBitSet(light.EmptySkyLightMask);
        writer.WriteBitSet(light.EmptyBlockLightMask);

        writer.WriteVarInt(light.SkyLightArrays.Length);
        foreach (byte[] array in light.SkyLightArrays)
        {
            writer.WritePrefixedByteArray(array);
        }

        writer.WriteVarInt(light.BlockLightArrays.Length);
        foreach (byte[] array in light.BlockLightArrays)
        {
            writer.WritePrefixedByteArray(array);
        }
    }

    private static LightData ReadLightData(ref PacketReader reader)
    {
        long[] skyMask = reader.ReadBitSet();
        long[] blockMask = reader.ReadBitSet();
        long[] emptySkyMask = reader.ReadBitSet();
        long[] emptyBlockMask = reader.ReadBitSet();

        byte[][] skyArrays = ReadLightArrays(ref reader);
        byte[][] blockArrays = ReadLightArrays(ref reader);

        return new LightData(skyMask, blockMask, emptySkyMask, emptyBlockMask, skyArrays, blockArrays);
    }

    private static byte[][] ReadLightArrays(ref PacketReader reader)
    {
        int count = reader.ReadVarInt();
        if (count < 0)
        {
            throw new InvalidDataException($"Negative light array count ({count}).");
        }

        var arrays = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            arrays[i] = reader.ReadPrefixedByteArray();
        }

        return arrays;
    }
}
