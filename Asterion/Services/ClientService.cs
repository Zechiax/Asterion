using Asterion.Database;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.Services;

public class ClientService
{
    private readonly DiscordSocketClient _client;
    private readonly IServiceProvider _services;

    public ClientService(IServiceProvider serviceProvider)
    {
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _services = serviceProvider;
    }

    public void Initialize()
    {
        _client.Ready += SetGameAsync;
    }

    public async Task SetGameAsync()
    {
        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var count = await db.ModrinthProjects.CountAsync();

        await _client.SetGameAsync(
            $"Monitoring {count} project{(count == 1 ? null : 's')} for updates in {_client.Guilds.Count} servers");
    }
}
