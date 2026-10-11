using Microsoft.Extensions.Logging;
using VoXelSharp.Protocol.Packets;
using VoXelSharp.Server.Status;

namespace VoXelSharp.Server;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private readonly MinecraftServer _server = Init(logger);

    private static MinecraftServer Init(ILogger logger)
    {
        int port = 25565;
        int maxPlayers = 20;
        string description = "A VoXelSharp Server";

        var registry = PacketRegistry.CreateDefault();
        var statusProvider = new ServerStatusJsonProvider(new ServerStatusData(description, maxPlayers, 0));
        // World persists under data/world (matches the Docker compose volume ./data/world:/app/data/world).
        string worldDirectory = Path.Combine(Directory.GetCurrentDirectory(), "data", "world");
        return new MinecraftServer(registry, statusProvider, logger, port, worldDirectory: worldDirectory);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _server.Start();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        finally
        {
            await _server.StopAsync();
        }
    }
}
