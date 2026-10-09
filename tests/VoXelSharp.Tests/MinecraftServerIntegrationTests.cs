using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server;
using VoXelSharp.Server.Configuration;
using VoXelSharp.Server.Status;
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

            // Acknowledge Finish Configuration -> the session enters Play and stays open.
            await SendFrameAsync(stream, AcknowledgeFinishConfigurationCodec.Instance,
                new AcknowledgeFinishConfigurationPacket(), ct);

            // The session is in Play; no further frames are sent unprompted. Give the session
            // a moment, then confirm the TCP connection is still open (not closed by the server).
            await Task.Delay(50, ct);
            Assert.True(client.Connected, "Server closed the connection instead of entering Play.");
        }
        finally
        {
            await server.StopAsync();
        }
    }

    // ---- helpers ----

    private sealed class FrameReader
    {
        private readonly NetworkStream _stream;
        private readonly byte[] _buffer = new byte[32 * 1024];
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
