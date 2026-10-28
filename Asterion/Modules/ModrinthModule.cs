using System.Text.RegularExpressions;
using Asterion.Attributes;
using Asterion.AutocompleteHandlers;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Extensions;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Exceptions;
using Modrinth.Models;

// ReSharper disable MemberCanBePrivate.Global

namespace Asterion.Modules;

[RequireContext(ContextType.Guild)]
// ReSharper disable once ClassNeverInstantiated.Global
public class ModrinthModule : AsterionInteractionModuleBase
{
    private const int SearchQueryMaxLength = 60;

    private readonly DiscordSocketClient _client;
    private readonly IGuildService _guildService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<ModrinthModule> _logger;
    private readonly ModrinthService _modrinthService;
    private readonly ILocalizationService _localizationService;
    private readonly IModrinthClient _modrinthClient;

    public ModrinthModule(IModrinthClient modrinthClient, IServiceProvider serviceProvider, ILocalizationService localizationService, IGuildCultureProvider cultureProvider) : base(localizationService, cultureProvider)
    {
        _modrinthClient = modrinthClient;
        _localizationService = serviceProvider.GetRequiredService<ILocalizationService>();
        _guildService = serviceProvider.GetRequiredService<IGuildService>();
        _subscriptionService = serviceProvider.GetRequiredService<ISubscriptionService>();
        _modrinthService = serviceProvider.GetRequiredService<ModrinthService>();
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _logger = serviceProvider.GetRequiredService<ILogger<ModrinthModule>>();
    }

    public ButtonBuilder GetSubscribeButtons(string projectId, bool subEnabled = true)
    {
        return ModrinthComponentBuilder.GetSubscribeButtons(Context.User.Id, projectId, subEnabled);
    }

    [SlashCommand("user", "Finds information about user, search by ID or username")]
    public async Task FindUser([Summary("Query", "ID or username")] [MaxLength(SearchQueryMaxLength)] string query)
    {
        await DeferAsync();
        _logger.LogDebug("Search for user '{Query}", query);
        var searchResult = await _modrinthService.FindUser(query);
        _logger.LogDebug("Search status: {SearchStatus}", searchResult.SearchStatus);

        if (!searchResult.Success)
        {
            await SendStatusAsync(Errors.FromSearch(searchResult));
            return;
        }

        var userDto = searchResult.Payload;

        var embed = ModrinthEmbedBuilder.GetUserEmbed(searchResult);

        var components = new ComponentBuilder()
            .WithButton(ModrinthComponentBuilder.GetUserLinkButton(userDto.User))
            .WithButton(FollowComponentBuilder.GetFollowUserButton(userDto.User, LocalizationService,
                CommandCultureInfo));

        await FollowupAsync(embed: embed.Build(), components: components.Build());
    }

    [SlashCommand("search", "Search Projects (by slug, ID or search) and gives you info about the first response")]
    public async Task SearchProject([Summary("Query", "Query, ID or slug")] [MaxLength(SearchQueryMaxLength)] string query)
    {
        await DeferAsync();
        _logger.LogDebug("Search for query '{Query}'", query);
        var searchResult = await _modrinthService.FindProject(query);
        _logger.LogDebug("Search status: {SearchStatus}", searchResult.SearchStatus);

        if (searchResult.Success == false)
        {
            await SendStatusAsync(Errors.FromSearch(searchResult));
            return;
        }

        var projectDto = searchResult.Payload;
        var project = projectDto.Project;

        var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

        var guild = await _guildService.GetGuildAsync(Context.Guild.Id);

        if (guild is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        var subscribedToProject = await _subscriptionService.IsSubscribedAsync(Context.Guild.Id, project.Id);
        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = ModrinthEmbedBuilder.GetProjectEmbed(searchResult, team).Build();
            var components = new ComponentBuilder();

            if ((bool) guild.GuildSettings.ShowSubscribeButton!)
                components.WithButton(GetSubscribeButtons(project.Id,
                    !subscribedToProject));

            components.WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project))
                .WithButton(ModrinthComponentBuilder.GetUserToViewButton(Context.User.Id, team.GetOwner()?.User.Id,
                    project.Id))
                .WithButton(ModrinthComponentBuilder.ViewMoreSearchResults(projectDto.SearchResponse, query), 1);

            x.Components = components.Build();
        });
    }

    [MessageCommand("Find project on Modrinth")]
    public async Task SearchOnModrinth(IMessage msg)
    {
        // Same limit as /search, newlines and '|' would break the custom ID of the "more results" button
        var query = Regex.Replace(msg.Content ?? string.Empty, @"[\s|]+", " ")
            .Truncate(SearchQueryMaxLength, string.Empty)
            .Trim();

        if (query.Length == 0)
        {
            await SendStatusAsync(Errors.MessageEmpty);
            return;
        }

        await SearchProject(query);
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [SlashCommand("subscribe", "Add a Modrinth project to your watched list")]
    public async Task Subscribe(string projectId, [AnnouncementChannelPrecondition] IGuildChannel? customChannel = null)
    {
        await DeferAsync();

        var channel = customChannel is null
            ? Context.Guild.GetTextChannel(Context.Channel.Id)
            : customChannel as ITextChannel;

        if (channel is null)
        {
            await SendStatusAsync(Errors.InvalidChannel);
            return;
        }

        var query = Subscriptions.ParseProjectInput(projectId);
        var result = await Subscriptions.SubscribeAsync(_modrinthService, _subscriptionService, Context.Guild.Id, query,
            channel.Id);

        switch (result.Status)
        {
            case SubscribeStatus.AlreadySubscribed:
                await SendStatusAsync(Warnings.AlreadySubscribed);
                return;
            case SubscribeStatus.ProjectNotFound:
                await SendStatusAsync(Errors.ProjectNotFound(query));
                return;
            case not SubscribeStatus.Subscribed:
                await SendStatusAsync(Errors.Internal);
                return;
        }

        await ModifyOriginalResponseAsync(x =>
        {
            x.Content = LocalizationService.Get("manage-subscribed", CommandCultureInfo,
                new { title = Format.Sanitize(result.Project!.Title), channel = channel.Mention });
        });

        if (!BotCanSendIn(channel.Id))
            await SendStatusAsync(Warnings.BotCannotSend(channel.Mention));
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [SlashCommand("unsubscribe", "Remove a Modrinth project from your watched list")]
    public async Task Unsubscribe(
        [Summary("project", "ID, slug or link of the project")]
        [Autocomplete(typeof(SubscribedIdAutocompletionHandler))]
        string project)
    {
        await DeferAsync();

        var lookup = await Subscriptions.FindSubscriptionAsync(_modrinthService, _subscriptionService, Context.Guild.Id,
            project);
        if (lookup.Found is not { } entry)
        {
            await SendStatusAsync(lookup.Error ?? Warnings.AlreadyUnsubscribed);
            return;
        }

        var question = LocalizationService.Get("entry-unsub-confirm-question", CommandCultureInfo,
            new { title = Format.Sanitize(entry.Project.Title ?? entry.ProjectId), id = entry.ProjectId });

        await FollowupAsync(components: SettingsPanelBuilder.BuildMessage(question,
            SettingsPanelBuilder.SubscriptionAccent,
            new ButtonBuilder(LocalizationService.Get("button-confirm", CommandCultureInfo),
                $"{UnsubscribeConfirmButtonId}:{Context.User.Id}:{entry.ProjectId}", ButtonStyle.Danger),
            new ButtonBuilder(LocalizationService.Get("button-cancel", CommandCultureInfo),
                $"{UnsubscribeCancelButtonId}:{Context.User.Id}:{entry.ProjectId}", ButtonStyle.Secondary)),
            flags: MessageFlags.ComponentsV2);
    }

    // "{id}:{ownerId}:{projectId}", only the user who ran /unsubscribe can confirm
    public const string UnsubscribeConfirmButtonId = "unsub-cmd-confirm";
    public const string UnsubscribeCancelButtonId = "unsub-cmd-cancel";

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [DoUserCheck]
    [ComponentInteraction(UnsubscribeConfirmButtonId + ":*:*")]
    public async Task HandleUnsubscribeConfirmAsync(ulong ownerId, string projectId)
    {
        await DeferAsync();

        // Also removes the shared project state if nothing else uses it
        var removed = await _subscriptionService.RemoveSubscriptionAsync(Context.Guild.Id, projectId);
        var text = removed is not null
            ? LocalizationService.Get("entry-unsubscribed", CommandCultureInfo,
                new { title = Format.Sanitize(removed.Project?.Title ?? projectId), id = projectId })
            : LocalizationService.Get("entry-not-found", CommandCultureInfo);

        await ModifyOriginalResponseAsync(msg =>
            msg.Components = SettingsPanelBuilder.BuildMessage(text, SettingsPanelBuilder.SubscriptionAccent));
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [DoUserCheck]
    [ComponentInteraction(UnsubscribeCancelButtonId + ":*:*")]
    public async Task HandleUnsubscribeCancelAsync(ulong ownerId, string projectId)
    {
        await DeferAsync();
        await ModifyOriginalResponseAsync(msg => msg.Components = SettingsPanelBuilder.BuildMessage(
            LocalizationService.Get("removal-cancelled", CommandCultureInfo),
            SettingsPanelBuilder.SubscriptionAccent));
    }

    [SlashCommand("latest-release", "Gets the latest release of project")]
    public async Task LatestRelease(string slugOrId,
        [Summary("style", "Message style, the server's message style if not set")] MessageStyle? style = null)
    {
        await DeferAsync();
        var project = await _modrinthService.GetProject(slugOrId);

        if (project == null)
        {
            await SendStatusAsync(Errors.ProjectNotFound(slugOrId));
            return;
        }

        var latestVersion = await _modrinthService.GetProjectsLatestVersion(project);

        if (latestVersion == null)
        {
            await SendStatusAsync(Errors.Unknown);
            return;
        }

        var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

        var guild = await _guildService.GetGuildAsync(Context.Guild.Id);

        if (guild is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }
        
        var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(guild.GuildSettings, project, latestVersion, team, style);

        var buttons =
            new ComponentBuilder().WithButton(
                ModrinthComponentBuilder.GetVersionUrlButton(project, latestVersion));

        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = buttons.Build();
        });
    }

    [SlashCommand("random", "Gets a random project")]
    public async Task GetRandomProject()
    {
        await DeferAsync();
        
        Project? randomProject;
        try
        {
            // Count to 70, as Modrinth currently returns less than 70 projects
            // If it's set to 1, it will sometimes return 0 projects
            randomProject = (await _modrinthClient.Project.GetRandomAsync(70)).FirstOrDefault();
        }
        catch (ModrinthApiException e)
        {
            _logger.LogError(e, "Error getting random project");
            throw new Exception("Error getting random project");
        }

        if (randomProject == null) throw new Exception("No projects found");

        var team = await _modrinthClient.Team.GetAsync(randomProject.Team);

        var embed = ModrinthEmbedBuilder.GetProjectEmbed(randomProject, team);

        var isSubscribed = await _subscriptionService.IsSubscribedAsync(Context.Guild.Id, randomProject.Id);

        var buttons = ModrinthComponentBuilder.GetSubscribeButtons(Context.User.Id, randomProject.Id, !isSubscribed);
        var ownerButtons =
            ModrinthComponentBuilder.GetUserToViewButton(Context.User.Id, team.GetOwner()?.User.Id,
                randomProject.Id);
        var viewButton = ModrinthComponentBuilder.GetProjectLinkButton(randomProject);

        var componentBuilder = new ComponentBuilder()
            .WithButton(buttons)
            .WithButton(viewButton)
            .WithButton(ownerButtons);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = componentBuilder.Build();
        });
    }

#if RELEASE
    [RequireOwner]
#endif
    [SlashCommand("force-update", "Forces check for updates")]
    public async Task ForceUpdate()
    {
        await DeferAsync(true);
        var ok = _modrinthService.ForceUpdate();
        await FollowupAsync($"Forced Update request: {ok}");
    }
}