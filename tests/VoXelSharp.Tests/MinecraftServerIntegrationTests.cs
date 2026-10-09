using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server;
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

    // ---- helpers ----

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
