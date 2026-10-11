using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Chunk;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server;
using VoXelSharp.Server.Configuration;
using VoXelSharp.Server.Status;
using VoXelSharp.World;
using Xunit;

namespace VoXelSharp.Tests;

/// <summary>
/// Integration tests for <see cref="MinecraftServer"/> over real TCP.
/// AGENTS P3: these are integration/smoke tests, not per-packet unit tests.
/// </summary>
public class MinecraftServerIntegrationTests
{
    [Fact]
    public async Task StatusRequestOverTcp()
    {
        var statusProvider = new ServerStatusJsonProvider(
            new ServerStatusData("A Minecraft Server", 20, 0));
        var registry = PacketRegistry.CreateDefault();
        var server = new MinecraftServer(registry, statusProvider, NullLogger<MinecraftServer>.Instance, 0);
        CancellationToken ct = TestContext.Current.CancellationToken;

        server.Start();
        int port = server.Port;

        using TcpClient client = new();
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), ct);

        try
        {
            NetworkStream stream = client.GetStream();

            // Client sends Handshake (Status intent)
            await SendFrameAsync(stream, HandshakeCodec.Instance,
                new HandshakePacket(777, "localhost", 25565, HandshakeIntent.Status), ct);

            // Client sends Status Request
            await SendFrameAsync(stream, StatusRequestCodec.Instance,
                new StatusRequestPacket(), ct);

            // Read Status Response frame
            byte[] payload = await ReadFrameAsync(stream, ct);

            var response = Assert.IsType<StatusResponsePacket>(
                PacketRegistry.CreateDefault().Decode(ConnectionState.Status, PacketDirection.Clientbound, payload));
            Assert.Contains("\"protocol\":777", response.Json);
            Assert.Contains("\"name\":\"26.3\"", response.Json);
            Assert.Contains("\"description\":{\"text\":\"A Minecraft Server\"}", response.Json);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task FullLoginConfigurationFlowOverTcp()
    {
        var statusProvider = new ServerStatusJsonProvider(
            new ServerStatusData("A Minecraft Server", 20, 0));
        var registry = PacketRegistry.CreateDefault();
        var server = new MinecraftServer(registry, statusProvider, NullLogger<MinecraftServer>.Instance, 0);
        CancellationToken ct = TestContext.Current.CancellationToken;

        server.Start();
        int port = server.Port;

        using TcpClient client = new();
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), ct);

        try
        {
            NetworkStream stream = client.GetStream();
            var reader = new FrameReader(stream);

            // Handshake (Login) -> Login Start -> Login Success.
            await SendFrameAsync(stream, HandshakeCodec.Instance,
                new HandshakePacket(777, "localhost", 25565, HandshakeIntent.Login), ct);
            await SendFrameAsync(stream, LoginStartCodec.Instance,
                new LoginStartPacket("Player1", -6148914691236517206L, -4919883347990483973L), ct);

            byte[] successPayload = await reader.ReadFrameAsync(ct);
            var success = Assert.IsType<LoginSuccessPacket>(
                registry.Decode(ConnectionState.Login, PacketDirection.Clientbound, successPayload));
            Assert.Equal("Player1", success.Profile.Username);

            // Login Acknowledged -> server enters Configuration and sends Known Packs.
            await SendFrameAsync(stream, LoginAcknowledgedCodec.Instance, new LoginAcknowledgedPacket(), ct);

            byte[] knownPacksPayload = await reader.ReadFrameAsync(ct);
            var knownPacks = Assert.IsType<KnownPacksPacket>(
                registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, knownPacksPayload));
            Assert.Contains(knownPacks.Packs, p => p.Namespace == "minecraft" && p.ID == "core");

            // Client replies with its serverbound Known Packs (empty is valid).
            await SendFrameAsync(stream, ServerKnownPacksCodec.Instance, new KnownPacksPacket([]), ct);

            // Server sends Registry Data, Update Tags, Feature Flags, Finish Configuration.
            var provider = new ServerConfigurationDataProvider();
            for (int i = 0; i < provider.RegistryData.Count; i++)
            {
                byte[] registryPayload = await reader.ReadFrameAsync(ct);
                var registryData = Assert.IsType<RegistryDataPacket>(
                    registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, registryPayload));
                Assert.Equal(provider.RegistryData[i].RegistryId, registryData.RegistryId);
            }

            byte[] tagsPayload = await reader.ReadFrameAsync(ct);
            Assert.IsType<UpdateTagsPacket>(
                registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, tagsPayload));

            byte[] flagsPayload = await reader.ReadFrameAsync(ct);
            var flags = Assert.IsType<FeatureFlagsPacket>(
                registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, flagsPayload));
            Assert.Contains("minecraft:vanilla", flags.Features);

            byte[] finishPayload = await reader.ReadFrameAsync(ct);
            Assert.IsType<FinishConfigurationPacket>(
                registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, finishPayload));

            // Acknowledge Finish Configuration -> the session enters Play and sends the
            // full join sequence.
            await SendFrameAsync(stream, AcknowledgeFinishConfigurationCodec.Instance,
                new AcknowledgeFinishConfigurationPacket(), ct);

            await ConsumePlayJoinSequenceAsync(reader, registry, ct);

            // The session is in Play; the connection stays open.
            await Task.Delay(50, ct);
            Assert.True(client.Connected, "Server closed the connection instead of entering Play.");
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task PlayJoinDeliversFlatWorldChunksOverTcp()
    {
        var statusProvider = new ServerStatusJsonProvider(
            new ServerStatusData("A Minecraft Server", 20, 0));
        var registry = PacketRegistry.CreateDefault();
        var server = new MinecraftServer(registry, statusProvider, NullLogger<MinecraftServer>.Instance, 0);
        CancellationToken ct = TestContext.Current.CancellationToken;

        server.Start();
        int port = server.Port;

        using TcpClient client = new();
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), ct);

        try
        {
            NetworkStream stream = client.GetStream();
            var reader = new FrameReader(stream);

            // Complete the login + Configuration flow as in FullLoginConfigurationFlowOverTcp.
            await SendFrameAsync(stream, HandshakeCodec.Instance,
                new HandshakePacket(777, "localhost", 25565, HandshakeIntent.Login), ct);
            await SendFrameAsync(stream, LoginStartCodec.Instance,
                new LoginStartPacket("Player1", 1, 2), ct);
            await reader.ReadFrameAsync(ct); // Login Success
            await SendFrameAsync(stream, LoginAcknowledgedCodec.Instance, new LoginAcknowledgedPacket(), ct);
            await reader.ReadFrameAsync(ct); // Known Packs (clientbound)
            await SendFrameAsync(stream, ServerKnownPacksCodec.Instance, new KnownPacksPacket([]), ct);

            var provider = new ServerConfigurationDataProvider();
            for (int i = 0; i < provider.RegistryData.Count; i++)
            {
                await reader.ReadFrameAsync(ct); // Registry Data
            }

            await reader.ReadFrameAsync(ct); // Update Tags
            await reader.ReadFrameAsync(ct); // Feature Flags
            await reader.ReadFrameAsync(ct); // Finish Configuration
            await SendFrameAsync(stream, AcknowledgeFinishConfigurationCodec.Instance,
                new AcknowledgeFinishConfigurationPacket(), ct);

            // Chunks are sent in the join sequence: verify the center chunk decodes to a
            // flat-world terrain section (bedrock, dirt, grass).
            ChunkDataPacket centerChunk = await ReadChunkDataUntilCenterAsync(reader, registry, ct);
            Assert.Equal(0, centerChunk.ChunkX);
            Assert.Equal(0, centerChunk.ChunkZ);
            Assert.Equal(3, centerChunk.Heightmaps.Length);

            var sections = ChunkDataIO.DecodeSections(centerChunk.Data, FlatWorldGenerator.SectionCount);
            Assert.Equal(FlatWorldGenerator.SectionCount, sections.Length);
            Assert.Equal(4 * 16 * 16, sections[0].BlockCount); // bedrock + 2 dirt + grass per column
            Assert.Equal(new[] { BlockStateIds.Bedrock, BlockStateIds.Dirt, BlockStateIds.GrassBlock, BlockStateIds.Air },
                sections[0].Blocks.Palette);
            Assert.Equal(0, sections[0].Biomes.BitsPerEntry);
            Assert.Equal([FlatWorldGenerator.PlainsBiomeGlobalId], sections[0].Biomes.Palette);

            // The client confirms the teleport; the connection stays open.
            await SendFrameAsync(stream, ConfirmTeleportationCodec.Instance,
                new ConfirmTeleportationPacket(1, 0.5, -60.0, 0.5, 0f, 0f), ct);

            await Task.Delay(50, ct);
            Assert.True(client.Connected, "Server closed the connection after Play.");
        }
        finally
        {
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task M5BasicGameplayLoopOverTcp()
    {
        var statusProvider = new ServerStatusJsonProvider(
            new ServerStatusData("A Minecraft Server", 20, 0));
        var registry = PacketRegistry.CreateDefault();
        var server = new MinecraftServer(registry, statusProvider, NullLogger<MinecraftServer>.Instance, 0);
        CancellationToken ct = TestContext.Current.CancellationToken;

        server.Start();
        int port = server.Port;

        using TcpClient client = new();
        await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), ct);

        try
        {
            NetworkStream stream = client.GetStream();
            var reader = new FrameReader(stream);

            // ---- Login + Configuration into Play ----
            await SendFrameAsync(stream, HandshakeCodec.Instance,
                new HandshakePacket(777, "localhost", 25565, HandshakeIntent.Login), ct);
            await SendFrameAsync(stream, LoginStartCodec.Instance,
                new LoginStartPacket("Player1", 1, 2), ct);
            await reader.ReadFrameAsync(ct); // Login Success
            await SendFrameAsync(stream, LoginAcknowledgedCodec.Instance, new LoginAcknowledgedPacket(), ct);
            await reader.ReadFrameAsync(ct); // Known Packs (clientbound)
            await SendFrameAsync(stream, ServerKnownPacksCodec.Instance, new KnownPacksPacket([]), ct);

            var provider = new ServerConfigurationDataProvider();
            for (int i = 0; i < provider.RegistryData.Count; i++)
            {
                await reader.ReadFrameAsync(ct); // Registry Data
            }

            await reader.ReadFrameAsync(ct); // Update Tags
            await reader.ReadFrameAsync(ct); // Feature Flags
            await reader.ReadFrameAsync(ct); // Finish Configuration
            await SendFrameAsync(stream, AcknowledgeFinishConfigurationCodec.Instance,
                new AcknowledgeFinishConfigurationPacket(), ct);

            // Consume the initial join burst (done via connect confirmation) up to the teleport.
            await ConsumePlayJoinSequenceAsync(reader, registry, ct);
            await SendFrameAsync(stream, ConfirmTeleportationCodec.Instance,
                new ConfirmTeleportationPacket(1, 0.5, -60.0, 0.5, 0f, 0f), ct);

            // ---- Move next to the grass block at (0,-61,0) ----
            await SendFrameAsync(stream, SetPlayerPositionCodec.Instance,
                new SetPlayerPositionPacket(0.5, -60.0, 0.5, 0x01), ct);

            // ---- Break the grass block: Block Update, Add Entity (drop), Set Entity Data, Ack ----
            await SendFrameAsync(stream, PlayerActionCodec.Instance,
                new PlayerActionPacket(PlayerActionPacket.StopDestroyBlock, new BlockPosition(0, -61, 0), face: 3, sequence: 1), ct);

            var update = Assert.IsType<BlockUpdatePacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
            Assert.Equal(new BlockPosition(0, -61, 0), update.Location);
            Assert.Equal(BlockStateIds.Air, update.BlockStateId);

            var drop = Assert.IsType<AddEntityPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
            Assert.Equal(AddEntityPacket.EntityTypeItem, drop.EntityTypeId);

            Assert.IsType<SetEntityDataPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
            Assert.IsType<BlockChangedAckPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));

            // ---- Walk onto the drop to pick it up: Take Item Entity + Set Player Inventory ----
            await SendFrameAsync(stream, SetPlayerPositionCodec.Instance,
                new SetPlayerPositionPacket(0.3, -60.0, 0.3, 0x01), ct);

            var take = Assert.IsType<TakeItemEntityPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
            Assert.Equal(drop.EntityId, take.ItemEntityId);
            Assert.Equal(1, take.PlayerId);

            var inv = Assert.IsType<SetPlayerInventoryPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
            Assert.Equal(0, inv.Slot);
            Assert.Equal(1, inv.Count);

            // ---- Select the hotbar slot and place the held block ----
            await SendFrameAsync(stream, SetCarriedItemCodec.Instance, new SetCarriedItemPacket(0), ct);
            await SendFrameAsync(stream, UseItemOnCodec.Instance,
                new UseItemOnPacket(0, new BlockPosition(0, -61, 0), face: 1, 0.5f, 0.5f, 0.5f, false, false, sequence: 5), ct);

            var placed = Assert.IsType<BlockUpdatePacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
            Assert.Equal(new BlockPosition(0, -60, 0), placed.Location);
            Assert.Equal(BlockStateIds.GrassBlock, placed.BlockStateId);
            Assert.IsType<SetPlayerInventoryPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct))); // consumed
            Assert.IsType<BlockChangedAckPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));

            // ---- Chat: server echoes it back ----
            await SendFrameAsync(stream, ServerboundChatCodec.Instance,
                new ServerboundChatPacket("hello", 0, 0, null, new LastSeenMessageUpdate(0, [], 0x00)), ct);
            var chat = Assert.IsType<SystemChatPacket>(
                registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
            Assert.IsType<VoXelSharp.Protocol.Nbt.NbtString>(chat.Content);

            // ---- The connection stays open through the whole loop ----
            await Task.Delay(50, ct);
            Assert.True(client.Connected, "Server closed the connection during the M5 gameplay loop.");
        }
        finally
        {
            await server.StopAsync();
        }
    }

    /// <summary>Reads the server's Play join burst until the (0,0) center chunk is seen.</summary>
    private static async Task<ChunkDataPacket> ReadChunkDataUntilCenterAsync(
        FrameReader reader, PacketRegistry registry, CancellationToken ct)
    {
        while (true)
        {
            byte[] payload = await reader.ReadFrameAsync(ct);
            IPacket packet = registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, payload);
            if (packet is ChunkDataPacket chunk && chunk.ChunkX == 0 && chunk.ChunkZ == 0)
            {
                return chunk;
            }
        }
    }

    /// <summary>
    /// Consumes the entire Play join burst: Login (play), Player Info, Set Center Chunk,
    /// Set Default Spawn Position, Set Simulation Distance, Set Held Item, Chunk Batch Start,
    /// the (2r+1)^2 Chunk Data square, Chunk Batch Finished, Set Render Distance, and
    /// Synchronize Player Position.
    /// </summary>
    private static async Task ConsumePlayJoinSequenceAsync(
        FrameReader reader, PacketRegistry registry, CancellationToken ct)
    {
        const int radius = 4;
        int expectedChunkCount = (2 * radius + 1) * (2 * radius + 1);
        var expected = new Queue<Type>(new[]
        {
            typeof(LoginPlayPacket),
            typeof(PluginMessagePacket), // server brand
            typeof(PlayerInfoUpdatePacket),
            typeof(SetCenterChunkPacket),
            typeof(SetDefaultSpawnPositionPacket),
            typeof(SetSimulationDistancePacket),
            typeof(SetHeldItemPacket),
            typeof(ChunkBatchStartPacket),
        });

        // Chunk Data packets are contiguous in the middle of the burst.
        bool inChunkRun = false;
        int chunksSeen = 0;
        while (expected.Count > 0 || chunksSeen < expectedChunkCount)
        {
            byte[] payload = await reader.ReadFrameAsync(ct);
            IPacket packet = registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, payload);

            if (packet is ChunkDataPacket)
            {
                inChunkRun = true;
                chunksSeen++;
                continue;
            }

            if (inChunkRun)
            {
                // The chunk run is contiguous; any non-chunk packet ends it.
                Assert.True(chunksSeen == expectedChunkCount,
                    $"Chunk run ended early: expected {expectedChunkCount}, saw {chunksSeen}.");
                inChunkRun = false;
            }

            Type actual = packet.GetType();
            Assert.True(expected.Count > 0, $"Unexpected packet in Play join: {actual.Name}.");
            Assert.Equal(expected.Dequeue(), actual);
        }

        // Chunk Batch Finished, Game Event (LEVEL_CHUNKS_LOAD_START), Set Render Distance,
        // Synchronize Player Position.
        Assert.IsType<ChunkBatchFinishedPacket>(
            registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
        Assert.IsType<GameEventPacket>(
            registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
        Assert.IsType<SetRenderDistancePacket>(
            registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
        Assert.IsType<SynchronizePlayerPositionPacket>(
            registry.Decode(ConnectionState.Play, PacketDirection.Clientbound, await reader.ReadFrameAsync(ct)));
    }

    // ---- helpers ----

    private sealed class FrameReader
    {
        private readonly NetworkStream _stream;
        private byte[] _buffer = new byte[32 * 1024];
        private int _count;

        public FrameReader(NetworkStream stream) => _stream = stream;

        public async Task<byte[]> ReadFrameAsync(CancellationToken ct)
        {
            while (true)
            {
                // A single TCP read may deliver several frames; drain buffered frames first.
                if (FrameCodec.TryReadFrame(_buffer.AsSpan(0, _count), out byte[] buffered, out int bufferedConsumed))
                {
                    Compact(bufferedConsumed);
                    return buffered;
                }

                int received = await _stream.ReadAsync(_buffer.AsMemory(_count), ct);
                if (received == 0)
                {
                    throw new InvalidOperationException("Connection closed before frame complete.");
                }

                _count += received;

                // A single chunk frame can exceed the initial buffer (sky-light arrays are
                // ~49 KB); grow when a stalled frame needs more room.
                if (_count == _buffer.Length)
                {
                    Array.Resize(ref _buffer, _buffer.Length * 2);
                }

                if (FrameCodec.TryReadFrame(_buffer.AsSpan(0, _count), out byte[] payload, out int consumed))
                {
                    Compact(consumed);
                    return payload;
                }
            }
        }

        private void Compact(int consumed)
        {
            int remaining = _count - consumed;
            if (remaining > 0)
            {
                Buffer.BlockCopy(_buffer, consumed, _buffer, 0, remaining);
            }

            _count = remaining;
        }
    }

    private static async Task SendFrameAsync<TPacket>(
        NetworkStream stream, PacketCodec<TPacket> codec, TPacket packet, CancellationToken ct)
        where TPacket : IPacket
    {
        byte[] payload = EncodePayload(codec, packet);
        byte[] frame = MakeFrame(payload);
        await stream.WriteAsync(frame, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<byte[]> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[1024];
        int offset = 0;

        while (true)
        {
            int received = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (received == 0)
            {
                throw new InvalidOperationException("Connection closed before frame complete.");
            }

            offset += received;
            if (FrameCodec.TryReadFrame(buffer.AsSpan(0, offset), out byte[] payload, out int consumed))
            {
                return payload;
            }
        }
    }

    private static byte[] EncodePayload<TPacket>(PacketCodec<TPacket> codec, TPacket packet)
        where TPacket : IPacket
    {
        var writer = new PacketWriter();
        codec.Encode(packet, writer);
        return writer.ToArray();
    }

    private static byte[] MakeFrame(byte[] payload)
    {
        byte[] frame = new byte[payload.Length + FrameLength.MaxEncodedLength];
        int written = FrameCodec.EncodeFrame(payload, frame);
        return frame[..written].ToArray();
    }
}
