namespace VoXelSharp.Protocol.Packets;

/// <summary>
/// Set Center Chunk (play/clientbound, packet ID 96, official name "set_chunk_cache_center").
/// Tells the client the chunk coordinates of the loading area center.
/// Field order per docs/data/packets.json: Chunk X (VarInt), Chunk Z (VarInt).
/// Both IDs were verified as protocol_id 96 against the Mojang data-generator report.
/// </summary>
public sealed class SetCenterChunkPacket : IPacket
{
    public SetCenterChunkPacket(int chunkX, int chunkZ)
    {
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    /// <summary>Chunk X coordinate of the loading area center.</summary>
    public int ChunkX { get; }

    /// <summary>Chunk Z coordinate of the loading area center.</summary>
    public int ChunkZ { get; }
}

/// <summary>Codec for <see cref="SetCenterChunkPacket"/>.</summary>
public sealed class SetCenterChunkCodec : PacketCodec<SetCenterChunkPacket>
{
    /// <summary>Singleton access.</summary>
    public static SetCenterChunkCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 96;

    /// <inheritdoc />
    protected override void EncodeBody(SetCenterChunkPacket packet, PacketWriter writer)
    {
        writer.WriteVarInt(packet.ChunkX);
        writer.WriteVarInt(packet.ChunkZ);
    }

    /// <inheritdoc />
    protected override SetCenterChunkPacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt(), reader.ReadVarInt());
}

/// <summary>
/// Set Render Distance (play/clientbound, packet ID 97, official name "set_chunk_cache_radius").
/// Sets the client's view distance in chunks.
/// Field order per docs/data/packets.json: View Distance (VarInt), range 2-32.
/// Verified as protocol_id 97 against the Mojang data-generator report.
/// </summary>
public sealed class SetRenderDistancePacket : IPacket
{
    public SetRenderDistancePacket(int viewDistance)
    {
        ViewDistance = viewDistance;
    }

    /// <summary>Render distance in chunks (2-32).</summary>
    public int ViewDistance { get; }
}

/// <summary>Codec for <see cref="SetRenderDistancePacket"/>.</summary>
public sealed class SetRenderDistanceCodec : PacketCodec<SetRenderDistancePacket>
{
    /// <summary>Singleton access.</summary>
    public static SetRenderDistanceCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 97;

    /// <inheritdoc />
    protected override void EncodeBody(SetRenderDistancePacket packet, PacketWriter writer)
    {
        if (packet.ViewDistance < 2 || packet.ViewDistance > 32)
        {
            throw new InvalidDataException($"View distance {packet.ViewDistance} is outside the protocol range 2-32.");
        }

        writer.WriteVarInt(packet.ViewDistance);
    }

    /// <inheritdoc />
    protected override SetRenderDistancePacket DecodeBody(ref PacketReader reader)
        => new(reader.ReadVarInt());
}

/// <summary>
/// Chunk Batch Start (play/clientbound, packet ID 12, official name "chunk_batch_start").
/// Marks the start of a batch of chunks; the client defers rendering until Chunk Batch Finished.
/// No fields (docs/data/packets.json). Verified as protocol_id 12 against the report.
/// </summary>
public sealed class ChunkBatchStartPacket : IPacket
{
}

/// <summary>Codec for <see cref="ChunkBatchStartPacket"/>.</summary>
public sealed class ChunkBatchStartCodec : PacketCodec<ChunkBatchStartPacket>
{
    /// <summary>Singleton access.</summary>
    public static ChunkBatchStartCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 12;

    /// <inheritdoc />
    protected override void EncodeBody(ChunkBatchStartPacket packet, PacketWriter writer)
    {
    }

    /// <inheritdoc />
    protected override ChunkBatchStartPacket DecodeBody(ref PacketReader reader) => new();
}

/// <summary>
/// Chunk Batch Finished (play/clientbound, packet ID 11, official name "chunk_batch_finished").
/// Marks the end of a chunk batch begun by Chunk Batch Start.
/// Field order per docs/data/packets.json: Batch size (VarInt).
/// Verified as protocol_id 11 against the Mojang data-generator report.
/// </summary>
public sealed class ChunkBatchFinishedPacket : IPacket
{
    public ChunkBatchFinishedPacket(int batchSize)
    {
        if (batchSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size cannot be negative.");
        }

        BatchSize = batchSize;
    }

    /// <summary>Number of chunks in the batch just finished.</summary>
    public int BatchSize { get; }
}

/// <summary>Codec for <see cref="ChunkBatchFinishedPacket"/>.</summary>
public sealed class ChunkBatchFinishedCodec : PacketCodec<ChunkBatchFinishedPacket>
{
    /// <summary>Singleton access.</summary>
    public static ChunkBatchFinishedCodec Instance { get; } = new();

    /// <inheritdoc />
    public override ConnectionState State => ConnectionState.Play;

    /// <inheritdoc />
    public override PacketDirection Direction => PacketDirection.Clientbound;

    /// <inheritdoc />
    public override int PacketId => 11;

    /// <inheritdoc />
    protected override void EncodeBody(ChunkBatchFinishedPacket packet, PacketWriter writer)
        => writer.WriteVarInt(packet.BatchSize);

    /// <inheritdoc />
    protected override ChunkBatchFinishedPacket DecodeBody(ref PacketReader reader)
    {
        int batchSize = reader.ReadVarInt();
        if (batchSize < 0)
        {
            throw new InvalidDataException($"Negative chunk batch size ({batchSize}).");
        }

        return new ChunkBatchFinishedPacket(batchSize);
    }
}
