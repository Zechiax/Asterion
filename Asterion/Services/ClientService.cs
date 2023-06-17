using System.ComponentModel;
using System.Timers;
using Asterion.Interfaces;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Timer = System.Timers.Timer;

namespace Asterion.Services;

public class ClientService
{
    private readonly DiscordSocketClient _client;
    private readonly IDataService _data;
    private readonly ISchedulerFactory _schedulerFactory;

    public ClientService(IServiceProvider serviceProvider)
    {
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _data = serviceProvider.GetRequiredService<IDataService>();
        _schedulerFactory = serviceProvider.GetRequiredService<ISchedulerFactory>();
    }

    public void Initialize()
    {
        _client.Ready += SetGameAsync;
        ScheduleRefreshJob();
    }

    private async void ScheduleRefreshJob()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        
        IJobDetail job = JobBuilder.Create<RefreshJob>()
            .WithIdentity("RefreshJob", "group1")
            .Build();

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity("RefreshTrigger", "group1")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(15 * 60) // Every 15 minutes
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }

    public async Task SetGameAsync()
    {
        var count = (await _data.GetAllModrinthProjectsAsync()).Count;

        await _client.SetGameAsync(
            $"Monitoring {count} project{(count == 1 ? null : 's')} for updates in {_client.Guilds.Count} servers");
    }

    // Define your Quartz job class as an inner class of ClientService
    private class RefreshJob : IJob
    {
        private readonly ClientService _clientService;

        public RefreshJob(ClientService clientService)
        {
            _clientService = clientService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await _clientService.SetGameAsync();
        }
    }
}
