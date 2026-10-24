using Asterion.AutocompleteHandlers;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Asterion.Services.Notifications;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Modrinth;

namespace Asterion.Modules;

[RequireOwner]
public class BotManagement : AsterionInteractionModuleBase
{
    private readonly IDataService _dataService;
    private readonly IServiceProvider _services;

    public BotManagement(IServiceProvider serviceProvider, ILocalizationService localizationService, IGuildCultureProvider cultureProvider) : base(localizationService, cultureProvider)
    {
        _services = serviceProvider;
        _dataService = serviceProvider.GetRequiredService<IDataService>();
    }

#if DEBUG
    [SlashCommand("register", "Registers this guild to the bot", runMode: RunMode.Async)]
    public async Task Register()
    {
        await RespondAsync("Registering this guild", ephemeral: true);
        await _dataService.AddGuildAsync(Context.Guild.Id);
        await FollowupAsync("Registered", ephemeral: true);
    }

    [SlashCommand("unregister", "Un-registers this guild to the bot", runMode: RunMode.Async)]
    public async Task Unregister()
    {
        await RespondAsync("Unregistering this guild", ephemeral: true);
        await _dataService.RemoveGuildAsync(Context.Guild.Id);
        await FollowupAsync("Unregistered", ephemeral: true);
    }

    [SlashCommand("test", "general testing command")]
    public async Task Test([Autocomplete(typeof(GameVersionAutocompletionHandler))] string gameVersion)
    {
        await RespondAsync(gameVersion, ephemeral: true);
    }

    [SlashCommand("milestone-trigger", "Sends a milestone of a tracked project through the real pipeline",
        runMode: RunMode.Async)]
    public async Task MilestoneTrigger([Summary("project", "Slug or ID of a project tracked by the bot")] string query)
    {
        await DeferAsync(ephemeral: true);

        var project = await GetProjectAsync(query);
        if (project is null)
            return;

        using var scope = _services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var dbProject = await db.ModrinthProjects.FindAsync(project.Id);
        if (dbProject is null)
        {
            await FollowupAsync($"**{project.Title}** is not tracked, subscribe to it or follow its creator first",
                ephemeral: true);
            return;
        }

        var settings = (await _dataService.GetGuildByIdAsync(Context.Guild.Id))?.GuildSettings;
        var ladder = settings?.MilestoneLadder ?? MilestoneLadder.Standard;
        var milestone = DownloadMilestones.HighestCrossed(0, project.Downloads, ladder);
        if (milestone is null)
        {
            await FollowupAsync($"**{project.Title}** has {project.Downloads} downloads, below the first milestone " +
                                $"of the {ladder} ladder", ephemeral: true);
            return;
        }

        // Just below the milestone, so the pass sees the project's real downloads cross it
        dbProject.MilestoneCheckedDownloads = milestone.Value - 1;
        await db.SaveChangesAsync();

        var warning = settings is { MilestonesEnabled: true }
            ? ""
            : "\n:warning: Milestones are disabled for this server, nothing will be sent here";
        await FollowupAsync($"Set checked downloads of **{project.Title}** to {milestone.Value - 1}, running a " +
                            $"detection pass for the {milestone.Value} milestone{warning}", ephemeral: true);

        await _services.GetRequiredService<UpdateDetectionService>().TriggerImmediateCheckAsync();
        await FollowupAsync("Detection pass finished, the dispatcher sends the message within a few seconds",
            ephemeral: true);
    }

    [SlashCommand("milestone-preview", "Shows the milestone message of any project, nothing is saved or sent")]
    public async Task MilestonePreview(
        [Summary("project", "Slug or ID of the project")] string query,
        [Summary("milestone", "Milestone to show, the highest one the project reached if not set")]
        long? milestone = null)
    {
        await DeferAsync(ephemeral: true);

        var project = await GetProjectAsync(query);
        if (project is null)
            return;

        var team = await _services.GetRequiredService<IModrinthClient>().Team.GetAsync(project.Team);
        var shown = milestone ?? DownloadMilestones.HighestCrossed(0, project.Downloads, MilestoneLadder.Standard)
            ?? 1_000;

        await FollowupAsync(embed: ModrinthEmbedBuilder.MilestoneEmbed(project, shown, team).Build(),
            components: new ComponentBuilder()
                .WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project)).Build(),
            ephemeral: true);
    }

    private async Task<Modrinth.Models.Project?> GetProjectAsync(string query)
    {
        try
        {
            return await _services.GetRequiredService<IModrinthClient>().Project.GetAsync(query);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Could not get project {Format.Sanitize(query)}: {ex.Message}", ephemeral: true);
            return null;
        }
    }
#endif
}