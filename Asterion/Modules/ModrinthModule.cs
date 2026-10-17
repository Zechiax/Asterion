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
    private readonly DiscordSocketClient _client;
    private readonly IDataService _dataService;
    private readonly ILogger<ModrinthModule> _logger;
    private readonly ModrinthService _modrinthService;
    private readonly ILocalizationService _localizationService;
    private readonly IModrinthClient _modrinthClient;

    public ModrinthModule(IModrinthClient modrinthClient, IServiceProvider serviceProvider, ILocalizationService localizationService, IGuildCultureProvider cultureProvider) : base(localizationService, cultureProvider)
    {
        _modrinthClient = modrinthClient;
        _localizationService = serviceProvider.GetRequiredService<ILocalizationService>();
        _dataService = serviceProvider.GetRequiredService<IDataService>();
        _modrinthService = serviceProvider.GetRequiredService<ModrinthService>();
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

        var components = new ComponentBuilder()
            .WithButton(ModrinthComponentBuilder.GetUserLinkButton(userDto.User))
            .WithButton(FollowComponentBuilder.GetFollowUserButton(userDto.User, LocalizationService,
                CommandCultureInfo));

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

        var channel = customChannel is null
            ? Context.Guild.GetTextChannel(Context.Channel.Id)
            : customChannel as ITextChannel;

        if (channel is null)
        {
            await ModifyOriginalResponseAsync(x =>
            {
                x.Content = "There was an error processing your request, check if the channel is correct";
            });
            return;
        }

        var result = await Subscriptions.SubscribeAsync(_modrinthService, _dataService, Context.Guild.Id,
            Subscriptions.ParseProjectInput(projectId), channel.Id);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Content = result.Status switch
            {
                SubscribeStatus.Subscribed =>
                    $@"Subscribed to updates for project **{result.Project!.Title}** with ID **{result.Project.Id}** updates will be send to channel {channel.Mention} :white_check_mark:",
                SubscribeStatus.AlreadySubscribed => $"You're already subscribed to project ID {result.Project!.Id}",
                _ => "There was an error processing your request, check if the ID is correct or try again later"
            };
        });

        if (result.Status != SubscribeStatus.Subscribed)
            return;

        if (!BotCanSendIn(channel.Id))
            await FollowupAsync(
                ":warning: I don't have permission to send messages in this channel, please give me the required permissions :warning:",
                ephemeral: true);
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

        if (guild is null)
        {
            // Try again later
            await ModifyOriginalResponseAsync(x => { x.Content = "Internal error, please try again later"; });
            return;
        }
        
        var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(guild.GuildSettings, project, latestVersion, team);

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