using Microsoft.Extensions.Hosting;

namespace Asterion.Services;

public class BotActivityRefreshService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);

    private readonly ClientService _clientService;

    public BotActivityRefreshService(ClientService clientService)
    {
        _clientService = clientService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        do
        {
            await _clientService.SetGameAsync();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
