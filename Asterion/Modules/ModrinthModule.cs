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
using Fergun.Interactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth;
using Modrinth.Exceptions;
using Modrinth.Models;

// ReSharper disable MemberCanBePrivate.Global

namespace Asterion.Modules;

[EnabledInDm(false)]
[RequireContext(ContextType.Guild)]
// ReSharper disable once ClassNeverInstantiated.Global
public class ModrinthModule : AsterionInteractionModuleBase
{
    private readonly DiscordSocketClient _client;
    private readonly IDataService _dataService;
    private readonly InteractiveService _interactive;
    private readonly ILogger<ModrinthModule> _logger;
    private readonly ModrinthService _modrinthService;
    private readonly ILocalizationService _localizationService;
    private readonly IModrinthClient _modrinthClient;

    public ModrinthModule(IModrinthClient modrinthClient, IServiceProvider serviceProvider, ILocalizationService localizationService) : base(localizationService)
    {
        _modrinthClient = modrinthClient;
        _localizationService = serviceProvider.GetRequiredService<ILocalizationService>();
        _dataService = serviceProvider.GetRequiredService<IDataService>();
        _modrinthService = serviceProvider.GetRequiredService<ModrinthService>();
        _interactive = serviceProvider.GetRequiredService<InteractiveService>();
        _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
        _logger = serviceProvider.GetRequiredService<ILogger<ModrinthModule>>();
    }

    public ButtonBuilder GetSubscribeButtons(string projectId, bool subEnabled = true)
    {
        return ModrinthComponentBuilder.GetSubscribeButtons(Context.User.Id, projectId, subEnabled);
    }

    [SlashCommand("user", "Finds information about user, search by ID or username")]
    public async Task FindUser([Summary("Query", "ID or username")] [MaxLength(60)] string query)
    {
        await DeferAsync();
        _logger.LogDebug("Search for user '{Query}", query);
        var searchResult = await _modrinthService.FindUser(query);
        _logger.LogDebug("Search status: {SearchStatus}", searchResult.SearchStatus);
        
        switch (searchResult.SearchStatus)
        {
            case SearchStatus.ApiDown:
                await FollowupAsync("Modrinth API is probably down, please try again later");
                return;
            case SearchStatus.NoResult:
                await FollowupAsync($"No result for query '{query}'");
                return;
            case SearchStatus.UnknownError:
                await FollowupAsync("Unknown error, please try again later");
                return;
            case SearchStatus.FoundById:
                break;
            case SearchStatus.FoundBySearch:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var userDto = searchResult.Payload;

        var embed = ModrinthEmbedBuilder.GetUserEmbed(searchResult);

        var components =
            new ComponentBuilder().WithButton(ModrinthComponentBuilder.GetUserLinkButton(userDto.User));

        await FollowupAsync(embed: embed.Build(), components: components.Build());
    }

    [SlashCommand("search", "Search Projects (by slug, ID or search) and gives you info about the first response")]
    public async Task SearchProject([Summary("Query", "Query, ID or slug")] [MaxLength(60)] string query)
    {
        await DeferAsync();
        _logger.LogDebug("Search for query '{Query}'", query);
        var searchResult = await _modrinthService.FindProject(query);
        _logger.LogDebug("Search status: {SearchStatus}", searchResult.SearchStatus);

        if (searchResult.Success == false)
        {
            await FollowupWithSearchResultErrorAsync(searchResult);
            return;
        }

        var projectDto = searchResult.Payload;
        var project = projectDto.Project;

        var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

        if (guild is null)
        {
            // Try again later
            await ModifyOriginalResponseAsync(x => { x.Content = "Internal error, please try again later"; });
            return;
        }

        var subscribedToProject = await _dataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, project.Id);
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
        if (string.IsNullOrEmpty(msg.Content))
        {
            await RespondAsync("Message contains no content to search for", ephemeral: true);
            return;
        }

        await SearchProject(msg.Content);
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [SlashCommand("subscribe", "Add a Modrinth project to your watched list")]
    public async Task Subscribe(string projectId, [AnnouncementChannelPrecondition] IGuildChannel? customChannel = null)
    {
        await DeferAsync();
        var project = await _modrinthService.GetProject(projectId);

        var channel = customChannel is null
            ? Context.Guild.GetTextChannel(Context.Channel.Id)
            : customChannel as ITextChannel;

        if (project == null)
        {
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content =
                    "There was an error processing your request, check if the ID is correct or try again later";
            });
            return;
        }

        var versions = await _modrinthService.GetVersionListAsync(project.Id);
        if (versions == null)
        {
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content =
                    "There was an error processing your request, check if the ID is correct or try again later";
            });
            return;
        }

        // Get last version ID
        var lastVersion = versions.OrderByDescending(x => x.DatePublished).First().Id;

        if (channel is null)
        {
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = "There was an error processing your request, check if the channel is correct";
            });
            return;
        }

        await _dataService.AddModrinthProjectToGuildAsync(Context.Guild.Id, project.Id, lastVersion, project.Updated, channel.Id,
            project.Title);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Content =
                $@"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** updates will be send to channel {channel.Mention} :white_check_mark:";
        });

        // Check permissions, if the bot can send messages in the channel
        if (Context.Channel is ITextChannel textChannel && textChannel.Guild.GetUserAsync(Context.Client.CurrentUser.Id)
                .Result.GetPermissions(textChannel).SendMessages == false)
            await FollowupAsync(
                ":warning: I don't have permission to send messages in this channel, please give me the required permissions :warning:",
                ephemeral: true);
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [SlashCommand("change-channel", "Change channel for one of your subscribed projects")]
    public async Task ChangeChannel(
        [Summary("project_id")] [Autocomplete(typeof(SubscribedIdAutocompletionHandler))]
        string projectId,
        [AnnouncementChannelPrecondition] IGuildChannel newChannel)
    {
        await DeferAsync();

        var subscribed = await _dataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, projectId);

        if (!subscribed)
        {
            await ModifyOriginalResponseAsync(x => { x.Content = $"You're now subscribed to project ID {projectId}"; });
            return;
        }

        var success = await _dataService.ChangeModrinthEntryChannel(Context.Guild.Id, projectId, newChannel.Id);

        if (success)
        {
            var forMention = Context.Guild.GetTextChannel(newChannel.Id);
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content =
                    $"New updates for project {projectId} will be send to channel {forMention.Mention} :white_check_mark:";
            });
        }
        else
        {
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = "Something went wrong while changing the update channel, please try again later";
            });
        }
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [SlashCommand("unsubscribe", "Remove Modrinth project from your watched list")]
    public async Task Unsubscribe(
        [Summary("project_id")] [Autocomplete(typeof(SubscribedIdAutocompletionHandler))]
        string projectId)
    {
        await DeferAsync();
        var removed = await _dataService.RemoveModrinthProjectFromGuildAsync(Context.Guild.Id, projectId);

        if (removed == false)
        {
            await ModifyOriginalResponseAsync(x => { x.Content = $"Project with ID {projectId} is not subscribed"; });
            return;
        }

        await ModifyOriginalResponseAsync(x =>
        {
            x.Content = $"Unsubscribed from updates for project ID **{projectId}** :white_check_mark:";
        });
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [SlashCommand("unsubscribe-all", "Removes all subscribed projects")]
    public async Task UnsubscribeAll()
    {
        var builder = new ComponentBuilder()
            .WithButton("Yes", "remove-all-data-yes", ButtonStyle.Danger);

        await RespondAsync(
            "**Are you sure you want to remove all projects?** \n\n**Click the button if Yes, if not, just wait**",
            components: builder.Build());

        // Wait for a user to press the button
        var result = await _interactive.NextMessageComponentAsync(x => x.Data.Type == ComponentType.Button
                                                                       && x.Data.CustomId == "remove-all-data-yes"
                                                                       && Context.User.Id == x.User.Id
                                                                       && x.Message.Interaction.Id ==
                                                                       Context.Interaction.Id,
            timeout: TimeSpan.FromSeconds(15));


        if (result.IsSuccess)
        {
            // Acknowledge the interaction
            await result.Value!.DeferAsync();

            foreach (var versions in (await _dataService.GetAllGuildsSubscribedProjectsAsync(Context.Guild.Id))!)
                await _dataService.RemoveModrinthProjectFromGuildAsync(Context.Guild.Id, versions.ProjectId);
        }

        await ModifyOriginalResponseAsync(x =>
        {
            x.Content = result.IsSuccess ? "All data cleared" : "Action cancelled";
            x.Components = new ComponentBuilder().Build(); // No components
            x.AllowedMentions = AllowedMentions.None;
        });
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [SlashCommand("list", "Lists all your subscribed projects")]
    public async Task ListSubscribed()
    {
        await DeferAsync();
        var list = (await _dataService.GetAllGuildsSubscribedProjectsAsync(Context.Guild.Id))!
            .OrderBy(x => x.Project.Title).ToList();

        if (list.Count == 0)
        {
            await FollowupAsync("You are not subscribed to any projects");
            return;
        }

        var embeds = ListEmbedBuilder.CreateListEmbed(list);

        await FollowupAsync(embeds: embeds.ToArray());
    }

    [SlashCommand("latest-release", "Gets the latest release of project")]
    public async Task LatestRelease(string slugOrId, MessageStyle style = MessageStyle.Full)
    {
        await DeferAsync();
        var project = await _modrinthService.GetProject(slugOrId);

        if (project == null)
        {
            await ModifyOriginalResponseAsync(x => { x.Content = "Project not found"; });
            return;
        }

        var latestVersion = await _modrinthService.GetProjectsLatestVersion(project);

        if (latestVersion == null)
        {
            await ModifyOriginalResponseAsync(x => { x.Content = "Error"; });
            return;
        }

        var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

        var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(guild?.GuildSettings, project, latestVersion, team);

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

        var isSubscribed = await _dataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, randomProject.Id);

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