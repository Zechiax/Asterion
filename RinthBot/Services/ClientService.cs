using System.ComponentModel;
using System.Timers;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Timer = System.Timers.Timer;

namespace RinthBot.Services;

public class ClientService
{
    private readonly BackgroundWorker _refreshWorker;
    private readonly DiscordSocketClient _client;
    private readonly DataService _data;

    public ClientService(IServiceProvider serviceProvider)
    {
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _data = serviceProvider.GetRequiredService<DataService>();
        
        _refreshWorker = new BackgroundWorker();
        _refreshWorker.DoWork += RefreshAsync;
        
        // Refresh status every 30 minutes
        var checkTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
        checkTimer.Elapsed += checkTimer_Elapsed;
        checkTimer.Start();
    }

    public async Task InitializeAsync()
    {
        _client.Ready += SetGameAsync;
    }

    private async void RefreshAsync(object? sender, DoWorkEventArgs e)
    {
        await SetGameAsync();
    }

    private void checkTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!_refreshWorker.IsBusy) _refreshWorker.RunWorkerAsync();
    }

    public async Task SetGameAsync()
    {
        var count = _data.GetAllProjects().Count();
        
        await _client.SetGameAsync(
            name: $"{count} project{(count == 1 ? null : 's')} for updates",
            type: ActivityType.Watching);
    }
}