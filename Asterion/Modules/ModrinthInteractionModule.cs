using Asterion.Attributes;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Extensions;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Modrinth.Models;

namespace Asterion.Modules;

[RequireContext(ContextType.Guild)]
public class ModrinthInteractionModule : AsterionInteractionModuleBase
{
    private const string RequestError = "Sorry, there was an error processing your request, try again later";
    private readonly IDataService _dataService;
    private readonly IFollowService _followService;
    private readonly ModrinthService _modrinthService;

    public ModrinthInteractionModule(IServiceProvider serviceProvider, ILocalizationService localizationService, IGuildCultureProvider cultureProvider) :
        base(localizationService, cultureProvider)
    {
        _dataService = serviceProvider.GetRequiredService<IDataService>();
        _followService = serviceProvider.GetRequiredService<IFollowService>();
        _modrinthService = serviceProvider.GetRequiredService<ModrinthService>();
    }

    private ComponentBuilder GetButtons(Project project, GuildSettings guildSettings, bool subEnabled = true,
        IEnumerable<TeamMember>? team = null)
    {
        var components = new ComponentBuilder();

        if ((bool) guildSettings.ShowSubscribeButton!)
            components.WithButton(
                ModrinthComponentBuilder.GetSubscribeButtons(Context.User.Id, project.Id, subEnabled));

        components.WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project))
            .WithButton(ModrinthComponentBuilder.GetUserToViewButton(Context.User.Id,
                team.GetOwner()?.User.Id, project.Id));
        
        return components;
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [ComponentInteraction("sub-project:*;*", runMode: RunMode.Async)]
    public async Task SubProject(string userId, string projectId)
    {
        await DeferAsync();

        var guildId = Context.Guild.Id;
        var channel = Context.Guild.GetTextChannel(Context.Channel.Id);
        var guild = await _dataService.GetGuildByIdAsync(guildId);

        if (channel is null || guild is null)
        {
            await FollowupAsync(RequestError, ephemeral: true);
            return;
        }

        var result = await Subscriptions.SubscribeAsync(_modrinthService, _dataService, guildId, projectId, channel.Id);

        if (result.Status is SubscribeStatus.ProjectNotFound or SubscribeStatus.Failed)
        {
            await FollowupAsync(RequestError, ephemeral: true);
            return;
        }

        var project = result.Project!;
        var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Components = GetButtons(project, guild.GuildSettings, false, team).Build();
        });

        if (result.Status == SubscribeStatus.AlreadySubscribed)
        {
            await FollowupAsync("You're already subscribed to updates for this project", ephemeral: true);
            return;
        }

        await FollowupAsync(
            $"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** :white_check_mark: Updates will be send to this channel {channel.Mention}",
            ephemeral: true);

        if (!BotCanSendIn(channel.Id))
            await FollowupAsync(
                ":warning: I don't have permission to send messages in this channel, please give me the required permissions :warning:",
                ephemeral: true);
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [ComponentInteraction("unsub-project:*;*", runMode: RunMode.Async)]
    public async Task UnsubProject(string userId, string projectId)
    {
        await DeferAsync();

        var guildId = Context.Guild.Id;

        var project = await _modrinthService.GetProject(projectId);
        var guild = await _dataService.GetGuildByIdAsync(guildId);

        if (guild is null)
        {
            // Try again later
            await FollowupAsync(RequestError, ephemeral: true);
            return;
        }

        // BUG: If the project does not exists, it will always throw an error
        if (project == null)
        {
            await FollowupAsync(RequestError, ephemeral: true);
            return;
        }
        
        var subscribed = await _dataService.IsGuildSubscribedToProjectAsync(guildId, project.Id);        

        var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

        // Already unsubscribed
        if (subscribed == false)
        {
            await ModifyOriginalResponseAsync(x =>
            {
                x.Components = GetButtons(project, guild.GuildSettings, team: team).Build();
            });
            await FollowupAsync("You're already unsubscribed from updates to this project", ephemeral: true);
            return;
        }

        await _dataService.RemoveModrinthProjectFromGuildAsync(guildId, projectId);

        await ModifyOriginalResponseAsync(x =>
        {
            x.Components = GetButtons(project, guild.GuildSettings, team: team).Build();
        });

        await FollowupAsync($"Unsubscribed from updates for project ID **{projectId}** :white_check_mark:",
            ephemeral: true);
    }

    [ComponentInteraction("show-user:*;*;*", runMode: RunMode.Async)]
    public async Task ShowUser(ulong discordUserId, string modrinthUserId, string projectId)
    {
        await DeferAsync(true);

        var findUser = await _modrinthService.FindUser(modrinthUserId);

        switch (findUser.SearchStatus)
        {
            case SearchStatus.ApiDown:
                await FollowupAsync("Modrinth API is down, please try again later", ephemeral: true);
                return;
            case SearchStatus.NoResult:
                await FollowupAsync("User does not exist", ephemeral: true);
                return;
            case SearchStatus.UnknownError:
                await FollowupAsync("There was an unknown error, please try again later", ephemeral: true);
                return;
            case SearchStatus.FoundById:
                break;
            case SearchStatus.FoundBySearch:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await FollowupAsync(embed: ModrinthEmbedBuilder.GetUserEmbed(findUser).Build(),
            components: new ComponentBuilder()
                //.WithButton(ModrinthComponentBuilder.BackToProjectButton(discordUserId, projectId))
                .WithButton(ModrinthComponentBuilder.GetUserLinkButton(findUser.Payload.User))
                .WithButton(FollowComponentBuilder.GetFollowUserButton(findUser.Payload.User, LocalizationService,
                    CommandCultureInfo))
                .Build(),
            ephemeral: true);
    }

    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    [ComponentInteraction(FollowComponentBuilder.FollowUserButtonId + ":*", runMode: RunMode.Async)]
    public async Task FollowUser(string modrinthUserId)
    {
        await DeferAsync(true);

        var findUser = await _modrinthService.FindUser(modrinthUserId);
        if (!findUser.Success)
        {
            await FollowupWithSearchResultErrorAsync(findUser);
            return;
        }

        if (Context.Channel is not ITextChannel channel)
        {
            await FollowupAsync(LocalizationService.Get("follow-channel-invalid", CommandCultureInfo),
                ephemeral: true);
            return;
        }

        // Every message of the follow would fail to send, don't add it
        if (!BotCanSendIn(channel.Id))
        {
            await FollowupAsync(LocalizationService.Get("no-send-permission", CommandCultureInfo,
                new { channel = channel.Mention }), ephemeral: true);
            return;
        }

        var user = findUser.Payload.User;
        var result = await _followService.AddFollowAsync(Context.Guild.Id, FollowTargetType.User, user.Id,
            user.Username, channel.Id, FollowEvents.ProjectUpdate | FollowEvents.NewProject);

        await FollowupAsync(FollowEmbedBuilder.DescribeAddResult(result, user.Username, channel.Mention,
            LocalizationService, CommandCultureInfo), ephemeral: true);
    }

    [ComponentInteraction("back-project:*;*", runMode: RunMode.Async)]
    public async Task BackToProject(ulong discordUserId, string projectId)
    {
        //await DeferAsync();

        var searchResult = await _modrinthService.FindProject(projectId);
        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

        if (guild is null)
        {
            // Try again later
            await FollowupAsync(RequestError, ephemeral: true);
            return;
        }

        switch (searchResult.SearchStatus)
        {
            case SearchStatus.ApiDown:
                await FollowupAsync("Modrinth API is probably down, please try again later", ephemeral: true);
                return;
            case SearchStatus.NoResult:
                await FollowupAsync("Project does not exist", ephemeral: true);
                return;
            case SearchStatus.UnknownError:
                await FollowupAsync("Unknown error, please try again later", ephemeral: true);
                return;
            case SearchStatus.FoundById:
                break;
            case SearchStatus.FoundBySearch:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var projectDto = searchResult.Payload;
        var project = projectDto.Project;

        var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

        var subscribedToProject = await _dataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, project.Id);
        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = ModrinthEmbedBuilder.GetProjectEmbed(searchResult, team).Build();
            x.Components = GetButtons(project, guild.GuildSettings, !subscribedToProject, team)
                .Build();
        });
    }

    [ComponentInteraction("view-project-from-search:*", runMode: RunMode.Async)]
    public async Task ViewProjectFromSearch(string projectId)
    {
        await DeferAsync();

        var search = await _modrinthService.FindProject(projectId);

        if (search.Success == false)
        {
            await FollowupAsync("Couldn't find this project", ephemeral: true);
            return;
        }

        await BackToProject(Context.User.Id, projectId);
    }

    [ComponentInteraction("more-results:|*|", runMode: RunMode.Async)]
    public async Task MoreResults(string query)
    {
        await DeferAsync();
        query = query.Replace('_', ' ');

        var search = await _modrinthService.FindProject(query);

        if (search.Payload.SearchResponse is null)
        {
            await FollowupAsync("Could not find any more projects", ephemeral: true);
            return;
        }

        var embed = ModrinthEmbedBuilder.GetMoreResultsEmbed(search.Payload.SearchResponse.Hits, query);

        await FollowupAsync(embed: embed.Build(),
            components: ModrinthComponentBuilder.GetResultSearchButton(search.Payload.SearchResponse.Hits).Build());
    }
}
