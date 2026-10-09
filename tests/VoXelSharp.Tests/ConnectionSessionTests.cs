using System.Buffers;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging.Abstractions;
using VoXelSharp.Protocol;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server.Configuration;
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
        var packet = Assert.IsType<StatusResponsePacket>(PacketRegistry.CreateDefault().Decode(ConnectionState.Status, PacketDirection.Clientbound, payload));

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
        var packet = Assert.IsType<PongResponsePacket>(PacketRegistry.CreateDefault().Decode(ConnectionState.Status, PacketDirection.Clientbound, payload));
        Assert.Equal(timestamp, packet.Timestamp);

        // The wiki flow closes the connection after Pong.
        await harness.WaitForSessionAsync();
    }

    [Fact]
    public async Task LoginFlowReachesConfiguration()
    {
        const string playerName = "Player1";
        const long uuidMsb = 0x0123456789ABCDEF;
        const long uuidLsb = -2742139916407285296L;

        await using var harness = new SessionHarness();

        // Client: Handshake (Login) then Login Start.
        await harness.ClientSendAsync(HandshakeCodec.Instance, new HandshakePacket(777, "x", 25565, HandshakeIntent.Login));
        await harness.ClientSendAsync(LoginStartCodec.Instance, new LoginStartPacket(playerName, uuidMsb, uuidLsb));

        // Server responds with Login Success echoing the player name and UUID.
        byte[] successPayload = await harness.ReadServerFrameAsync();
        var success = Assert.IsType<LoginSuccessPacket>(
            PacketRegistry.CreateDefault().Decode(ConnectionState.Login, PacketDirection.Clientbound, successPayload));
        Assert.Equal(playerName, success.Profile.Username);
        Assert.Equal(uuidMsb, success.Profile.PlayerUuidMsb);
        Assert.Equal(uuidLsb, success.Profile.PlayerUuidLsb);

        // Client confirms with Login Acknowledged; the session moves to Configuration.
        await harness.ClientSendAsync(LoginAcknowledgedCodec.Instance, new LoginAcknowledgedPacket());

        // The session advertises its known packs as the first Configuration step.
        byte[] knownPacksPayload = await harness.ReadServerFrameAsync();
        var knownPacks = Assert.IsType<KnownPacksPacket>(
            PacketRegistry.CreateDefault().Decode(ConnectionState.Configuration, PacketDirection.Clientbound, knownPacksPayload));
        Assert.Contains(knownPacks.Packs, p => p.Namespace == "minecraft" && p.ID == "core");

        await harness.AssertSessionOpenAsync();
    }

    [Fact]
    public async Task FullConfigurationFlowReachesPlay()
    {
        await using var harness = new SessionHarness();

        // Client: Handshake (Login) then Login Start.
        await harness.ClientSendAsync(HandshakeCodec.Instance, new HandshakePacket(777, "x", 25565, HandshakeIntent.Login));
        await harness.ClientSendAsync(LoginStartCodec.Instance, new LoginStartPacket("Player1", 1, 2));

        // Server: Login Success.
        await harness.ReadServerFrameAsync();

        // Client: Login Acknowledged -> Configuration.
        await harness.ClientSendAsync(LoginAcknowledgedCodec.Instance, new LoginAcknowledgedPacket());

        // Server: Known Packs (clientbound) - the first Configuration step.
        byte[] knownPacksPayload = await harness.ReadServerFrameAsync();
        var knownPacks = Assert.IsType<KnownPacksPacket>(
            PacketRegistry.CreateDefault().Decode(ConnectionState.Configuration, PacketDirection.Clientbound, knownPacksPayload));
        Assert.Single(knownPacks.Packs);
        Assert.Equal("minecraft", knownPacks.Packs[0].Namespace);
        Assert.Equal("core", knownPacks.Packs[0].ID);

        // Client: Known Packs (serverbound) - echoes what it knows; an empty list is valid
        // and forces the server to send all registry data (wiki "Known packs negotiation").
        await harness.ClientSendAsync(ServerKnownPacksCodec.Instance, new KnownPacksPacket([]));

        // Server: Registry Data for each synchronized registry.
        var registry = PacketRegistry.CreateDefault();
        var provider = new ServerConfigurationDataProvider();
        for (int i = 0; i < provider.RegistryData.Count; i++)
        {
            byte[] registryPayload = await harness.ReadServerFrameAsync();
            var registryData = Assert.IsType<RegistryDataPacket>(
                registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, registryPayload));
            Assert.Equal(provider.RegistryData[i].RegistryId, registryData.RegistryId);
        }

        // Server: Update Tags.
        byte[] updateTagsPayload = await harness.ReadServerFrameAsync();
        Assert.IsType<UpdateTagsPacket>(
            registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, updateTagsPayload));

        // Server: Feature Flags.
        byte[] featureFlagsPayload = await harness.ReadServerFrameAsync();
        var featureFlags = Assert.IsType<FeatureFlagsPacket>(
            registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, featureFlagsPayload));
        Assert.Contains("minecraft:vanilla", featureFlags.Features);

        // Server: Finish Configuration.
        byte[] finishPayload = await harness.ReadServerFrameAsync();
        Assert.IsType<FinishConfigurationPacket>(
            registry.Decode(ConnectionState.Configuration, PacketDirection.Clientbound, finishPayload));

        // Client: Acknowledge Finish Configuration -> Play.
        await harness.ClientSendAsync(AcknowledgeFinishConfigurationCodec.Instance, new AcknowledgeFinishConfigurationPacket());

        await harness.AssertStateAsync(ConnectionState.Play);
        await harness.AssertSessionOpenAsync();
    }

    [Fact]
    public async Task AcknowledgeBeforeKnownPacksExchangeClosesConnection()
    {
        await using var harness = new SessionHarness();

        // Client: Handshake (Login) then Login Start, then Login Acknowledged.
        await harness.ClientSendAsync(HandshakeCodec.Instance, new HandshakePacket(777, "x", 25565, HandshakeIntent.Login));
        await harness.ClientSendAsync(LoginStartCodec.Instance, new LoginStartPacket("Player1", 1, 2));
        await harness.ReadServerFrameAsync(); // Login Success
        await harness.ClientSendAsync(LoginAcknowledgedCodec.Instance, new LoginAcknowledgedPacket());
        await harness.ReadServerFrameAsync(); // Known Packs (clientbound)

        // The client acknowledges Finish Configuration before the known-packs exchange has
        // completed (stage AwaitingKnownPacks), which is a protocol violation.
        await harness.ClientSendAsync(AcknowledgeFinishConfigurationCodec.Instance, new AcknowledgeFinishConfigurationPacket());

        await harness.WaitForSessionAsync();
    }

    [Fact]
    public async Task TransferIntentClosesWithoutResponse()
    {
        await using var harness = new SessionHarness();

        await harness.ClientSendAsync(HandshakeCodec.Instance, new HandshakePacket(777, "x", 25565, HandshakeIntent.Transfer));

        // Transfer flow is not implemented; the session should finish on its own.
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
        var packet = Assert.IsType<StatusResponsePacket>(PacketRegistry.CreateDefault().Decode(ConnectionState.Status, PacketDirection.Clientbound, payload));
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
        private readonly ConnectionStateMachine _stateMachine;
        private readonly byte[] _received = new byte[4096];
        private int _receivedCount;

        public SessionHarness()
        {
            var serverTransport = new DuplexPipe(_clientToServer.Reader, _serverToClient.Writer);
            _stateMachine = new ConnectionStateMachine();
            var session = new ConnectionSession(
                serverTransport,
                PacketRegistry.CreateDefault(),
                _stateMachine,
                new ServerStatusJsonProvider(new ServerStatusData("A Minecraft Server", 20, 0)),
                new ServerConfigurationDataProvider(),
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
                // A single pipe read may deliver several server frames at once; drain any
                // frames already buffered locally before waiting for more pipe data.
                if (FrameCodec.TryReadFrame(_received.AsSpan(0, _receivedCount), out byte[] buffered, out int bufferedConsumed))
                {
                    Compact(bufferedConsumed);
                    return buffered;
                }

                ReadResult result;
                try
                {
                    result = await _serverToClient.Reader.ReadAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"No server frame arrived within the timeout. Session status: {_sessionTask.Status}, state: {_stateMachine.State}.");
                }

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

        public async Task AssertStateAsync(ConnectionState expected)
        {
            // Give the session a moment to process the last frame before asserting.
            await Task.Delay(50);
            Assert.Equal(expected, _stateMachine.State);
        }

        public async Task AssertSessionOpenAsync()
        {
            // Give the session a moment; it must NOT have completed on its own.
            await Task.Delay(50);
            Assert.False(_sessionTask.IsCompleted, "Session closed unexpectedly before Configuration was reached.");
        }

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
