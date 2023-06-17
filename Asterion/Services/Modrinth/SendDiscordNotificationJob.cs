using System.Text.Json;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Models;
using Quartz;
using Version = Modrinth.Models.Version;

namespace Asterion.Services.Modrinth;

public class SendDiscordNotificationJob : IJob
{
    private readonly ILogger<SendDiscordNotificationJob> _logger;
    private readonly IDataService _dataService;
    private readonly DiscordSocketClient _client;
    private readonly IModrinthClient _modrinthClient;

    private JobKey _jobKey;
    
    public SendDiscordNotificationJob(ILogger<SendDiscordNotificationJob> logger, IDataService dataService, DiscordSocketClient client, IModrinthClient modrinthClient)
    {
        _modrinthClient = modrinthClient;
        _client = client;
        _logger = logger;
        _dataService = dataService;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        _jobKey = context.JobDetail.Key;
        
        // We currently store the data there as JSON, so we need to deserialize it
        var projectJson = context.JobDetail.JobDataMap.GetString("project");
        var versionListJson = context.JobDetail.JobDataMap.GetString("versions");
        
        if (projectJson is null || versionListJson is null)
        {
            _logger.LogError("Project or version list was null");
            LogAbortJob();
            return;
        }

        var project = JsonSerializer.Deserialize<Project>(projectJson);
        var versionList = JsonSerializer.Deserialize<Version[]>(versionListJson);
        
        if (project is null || versionList is null)
        {
            _logger.LogError("Project or version list was null");
            LogAbortJob();
            return;
        }
        
        _logger.LogInformation("Starting to send notifications for {ProjectName} ({ProjectID}) with {NewVersionsCount} new versions", project.Title, project.Id, versionList.Length);
        await SendNotifications(project, versionList);
    }

    private void LogAbortJob()
    {
        _logger.LogWarning("Aborting job ID {JobId}", _jobKey);
    }

    private async Task SendNotifications(Project project, Version[] versions)
    {
        var guilds = await _dataService.GetAllGuildsSubscribedToProject(project.Id);

        if (guilds.Count <= 0)
        {
            _logger.LogWarning("No guilds subscribed to {ProjectName}, aborting job ID {JobId}", project.Title, _jobKey.Name);
            return;
        }

        // If the request fail, the job will be rescheduled so it should be fine to just throw here
        var team = await _modrinthClient.Team.GetAsync(project.Team);

        foreach (var guild in guilds)
        {
            var entry = await _dataService.GetModrinthEntryAsync(guild.GuildId, project.Id);
            
            if (entry is null)
            {
                _logger.LogWarning(
                    "No entry found for guild {GuildId} and project {ProjectId} (might have unsubscribed)",
                    guild.GuildId, project.Id);
                continue;
            }
            
            var channel = _client.GetGuild(guild.GuildId)?.GetTextChannel((ulong) entry.CustomUpdateChannel);
            
            if (channel is null)
            {
                _logger.LogWarning("Channel {ChannelId} not found in guild {GuildId}", entry.CustomUpdateChannel, guild.GuildId);
                // TODO: Maybe notify the user that the channel was not found and remove the entry?
                continue;
            }
            
            var pingRole = guild.PingRole is null ? null : channel.Guild.GetRole((ulong) guild.PingRole);
            
            foreach (var version in versions)
            {
                _logger.LogDebug("Sending notification for version {VersionId} of project {ProjectId} to guild {GuildId}", version.Id, project.Id, guild.GuildId);
                var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(guild.GuildSettings, project, version, team).Build();
                var buttons = new ComponentBuilder().WithButton(ModrinthComponentBuilder.GetVersionUrlButton(project, version)).Build();
                
                await SendUpdateEmbedToChannel(channel, pingRole?.Mention ?? string.Empty, embed, buttons);
            }
        }
    }

    private async Task SendUpdateEmbedToChannel(ISocketMessageChannel channel, string message, Embed embed, MessageComponent buttons)
    {
        try
        {
            await channel.SendMessageAsync(message, embed: embed, components: buttons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to channel {ChannelId}", channel.Id);
        }
    }
}