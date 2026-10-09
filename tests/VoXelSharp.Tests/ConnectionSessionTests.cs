using System.Buffers;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server.Connections;
using VoXelSharp.Server.Status;

namespace VoXelSharp.Tests;

public class ConnectionSessionTests
{
    [Fact]
    public async Task StatusRequestGetsStatusResponse()
    {
        await using var harness = new SessionHarness();

        // Client: Handshake (Status) then Status Request.
        await harness.ClientSendAsync(HandshakeCodec.Instance, new HandshakePacket(777, "localhost", 25565, HandshakeIntent.Status));
        await harness.ClientSendAsync(StatusRequestCodec.Instance, new StatusRequestPacket());

        byte[] payload = await harness.ReadServerFrameAsync();
        var packet = Assert.IsType<StatusResponsePacket>(PacketRegistry.CreateM2().Decode(ConnectionState.Status, PacketDirection.Clientbound, payload));

        Assert.Contains("\"protocol\":777", packet.Json);
        Assert.Contains("\"name\":\"26.3\"", packet.Json);
        Assert.Contains("\"description\":{\"text\":\"A Minecraft Server\"}", packet.Json);
    }

    [Fact]
    public async Task PingRequestGetsPongAndCloses()
    {
        const long timestamp = 0x0123456789ABCDEF;
        await using var harness = new SessionHarness();

        await harness.ClientSendAsync(HandshakeCodec.Instance, new HandshakePacket(777, "x", 25565, HandshakeIntent.Status));
        await harness.ClientSendAsync(PingRequestCodec.Instance, new PingRequestPacket(timestamp));

        byte[] payload = await harness.ReadServerFrameAsync();
        var packet = Assert.IsType<PongResponsePacket>(PacketRegistry.CreateM2().Decode(ConnectionState.Status, PacketDirection.Clientbound, payload));
        Assert.Equal(timestamp, packet.Timestamp);

        // The wiki flow closes the connection after Pong.
        await harness.WaitForSessionAsync();
    }

    [Fact]
    public async Task LoginIntentClosesWithoutResponse()
    {
        await using var harness = new SessionHarness();

        await harness.ClientSendAsync(HandshakeCodec.Instance, new HandshakePacket(777, "x", 25565, HandshakeIntent.Login));

        // Session should finish on its own (login flow not implemented in M2).
        await harness.WaitForSessionAsync();
        await harness.AssertNoMoreFramesAsync();
    }

    [Fact]
    public async Task MalformedFrameClosesConnection()
    {
        await using var harness = new SessionHarness();

        // Unknown serverbound packet ID 0x7F in the Handshaking state.
        byte[] bad = MakeFrame(new byte[] { 0x7F, 0x00 });
        await harness.ClientSendRawAsync(bad);

        await harness.WaitForSessionAsync();
    }

    [Fact]
    public async Task TruncatedFrameClosesConnection()
    {
        await using var harness = new SessionHarness();

        // Declares a length-prefixed frame of 5 bytes but sends only 3 bytes, then EOF.
        await harness.ClientSendRawAsync(new byte[] { 0x05, 0x00, 0x00 });
        await harness.ClientShutdownAsync();

        await harness.WaitForSessionAsync();
    }

    [Fact]
    public async Task FragmentedFramesAreReassembled()
    {
        await using var harness = new SessionHarness();

        // Split the Handshake frame across two writes.
        byte[] handshakeFrame = MakeFrame(
            PacketPayloadFactory(HandshakeCodec.Instance, new HandshakePacket(777, "localhost", 25565, HandshakeIntent.Status)));
        await harness.ClientSendRawAsync(handshakeFrame[..5]);
        await harness.ClientSendRawAsync(handshakeFrame[5..]);
        await harness.ClientSendAsync(StatusRequestCodec.Instance, new StatusRequestPacket());

        byte[] payload = await harness.ReadServerFrameAsync();
        var packet = Assert.IsType<StatusResponsePacket>(PacketRegistry.CreateM2().Decode(ConnectionState.Status, PacketDirection.Clientbound, payload));
        Assert.Contains("\"protocol\":777", packet.Json);
    }

    private static byte[] PacketPayloadFactory<TPacket>(PacketCodec<TPacket> codec, TPacket packet)
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

    private sealed class SessionHarness : IAsyncDisposable
    {
        private readonly Pipe _clientToServer = new();
        private readonly Pipe _serverToClient = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _sessionTask;
        private readonly byte[] _received = new byte[4096];
        private int _receivedCount;

        public SessionHarness()
        {
            var serverTransport = new DuplexPipe(_clientToServer.Reader, _serverToClient.Writer);
            var session = new ConnectionSession(
                serverTransport,
                PacketRegistry.CreateM2(),
                new ConnectionStateMachine(),
                new ServerStatusJsonProvider(new ServerStatusData("A Minecraft Server", 20, 0)),
                NullLogger<ConnectionSession>.Instance);
            _sessionTask = session.RunAsync(_cts.Token);
        }

        public async Task ClientSendAsync<TPacket>(PacketCodec<TPacket> codec, TPacket packet)
            where TPacket : IPacket
        {
            byte[] payload = PacketPayloadFactory(codec, packet);
            byte[] frame = MakeFrame(payload);
            await ClientSendRawAsync(frame);
        }

        public async Task ClientSendRawAsync(byte[] frame)
        {
            await _clientToServer.Writer.WriteAsync(frame);
            await _clientToServer.Writer.FlushAsync();
        }

        public Task ClientShutdownAsync()
        {
            _clientToServer.Writer.Complete();
            return Task.CompletedTask;
        }

        public async Task<byte[]> ReadServerFrameAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (true)
            {
                ReadResult result = await _serverToClient.Reader.ReadAsync(timeout.Token);
                Append(result.Buffer);
                _serverToClient.Reader.AdvanceTo(result.Buffer.End);

                if (FrameCodec.TryReadFrame(_received.AsSpan(0, _receivedCount), out byte[] payload, out int consumed))
                {
                    Compact(consumed);
                    return payload;
                }

                if (result.IsCompleted)
                {
                    throw new InvalidOperationException("Server closed the connection without sending a frame.");
                }
            }
        }

        public Task WaitForSessionAsync() => _sessionTask.WaitAsync(TimeSpan.FromSeconds(5));

        public async Task AssertNoMoreFramesAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            try
            {
                ReadResult result = await _serverToClient.Reader.ReadAsync(timeout.Token);
                Assert.True(result.IsCompleted, "Expected no further frames from the server.");
            }
            catch (OperationCanceledException)
            {
                _serverToClient.Reader.CancelPendingRead();
                // No frame arrived; treat as pass.
            }
        }

        private void Append(ReadOnlySequence<byte> buffer)
        {
            foreach (ReadOnlyMemory<byte> segment in buffer)
            {
                segment.Span.CopyTo(_received.AsSpan(_receivedCount));
                _receivedCount += segment.Length;
            }
        }

        private void Compact(int consumed)
        {
            int remaining = _receivedCount - consumed;
            if (remaining > 0)
            {
                Buffer.BlockCopy(_received, consumed, _received, 0, remaining);
            }

            _receivedCount = remaining;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _clientToServer.Writer.Complete();
            _serverToClient.Reader.Complete();
            await _serverToClient.Writer.CompleteAsync();
            _clientToServer.Reader.Complete();
            try
            {
                await _sessionTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
            }

            _cts.Dispose();
        }
    }

    private sealed class DuplexPipe : IDuplexPipe
    {
        public DuplexPipe(PipeReader input, PipeWriter output)
        {
            Input = input;
            Output = output;
        }

        public PipeReader Input { get; }

        public PipeWriter Output { get; }
    }
}
