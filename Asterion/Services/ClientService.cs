﻿using System.ComponentModel;
using System.Timers;
using Asterion.Interfaces;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Timer = System.Timers.Timer;

namespace Asterion.Services;

public class ClientService
{
    private readonly BackgroundWorker _refreshWorker;
    private readonly DiscordSocketClient _client;
    private readonly IDataService _data;

    public ClientService(IServiceProvider serviceProvider)
    {
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _data = serviceProvider.GetRequiredService<IDataService>();
        
        _refreshWorker = new BackgroundWorker();
        _refreshWorker.DoWork += RefreshAsync;
        
        // Refresh status every 15 minutes
        var checkTimer = new Timer(TimeSpan.FromMinutes(15).TotalMilliseconds);
        checkTimer.Elapsed += checkTimer_Elapsed;
        checkTimer.Start();
    }

    public void Initialize()
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
        var count = (await _data.GetAllModrinthProjectsAsync()).Count;

        await _client.SetGameAsync(
            name: $"Monitoring {count} project{(count == 1 ? null : 's')} for updates in {_client.Guilds.Count} servers");
    }
}