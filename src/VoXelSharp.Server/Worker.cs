namespace VoXelSharp.Server;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private const string _messageTemplate = "Worker running at: {Time}";
    private static readonly Action<ILogger, DateTimeOffset, Exception?> _workerRunning =
        LoggerMessage.Define<DateTimeOffset>(
            LogLevel.Information,
            new EventId(0, nameof(Worker)),
            _messageTemplate);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                _workerRunning(logger, DateTimeOffset.Now, null);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
