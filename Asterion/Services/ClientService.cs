using Asterion.Interfaces;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.Services;

public class ClientService
{
    private readonly DiscordSocketClient _client;
    private readonly IDataService _data;

    public ClientService(IServiceProvider serviceProvider)
    {
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _data = serviceProvider.GetRequiredService<IDataService>();
    }

    public void Initialize()
    {
        _client.Ready += SetGameAsync;
    }

    public async Task SetGameAsync()
    {
        var count = (await _data.GetAllModrinthProjectsAsync()).Count;

        await _client.SetGameAsync(
            $"Monitoring {count} project{(count == 1 ? null : 's')} for updates in {_client.Guilds.Count} servers");
    }
}
