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

        if (channel is null)
        {
            await SendStatusAsync(Errors.InvalidChannel);
            return;
        }

        if (guild is null)
        {
            await SendStatusAsync(Errors.Internal);
            return;
        }

        var result = await Subscriptions.SubscribeAsync(_modrinthService, _dataService, guildId, projectId, channel.Id);

        if (result.Status is SubscribeStatus.ProjectNotFound)
        {
            await SendStatusAsync(Errors.ProjectNotFound(projectId));
            return;
        }

        if (result.Status is SubscribeStatus.Failed)
        {
            await SendStatusAsync(Errors.Internal);
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
            await SendStatusAsync(Warnings.AlreadySubscribed);
            return;
        }

        await FollowupAsync(
            $"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** :white_check_mark: Updates will be send to this channel {channel.Mention}",
            ephemeral: true);

        if (!BotCanSendIn(channel.Id))
            await SendStatusAsync(Warnings.BotCannotSend(channel.Mention));
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
            await SendStatusAsync(Errors.Internal);
            return;
        }

        // BUG: If the project does not exists, it will always throw an error
        if (project == null)
        {
            await SendStatusAsync(Errors.ProjectNotFound(projectId));
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
            await SendStatusAsync(Warnings.AlreadyUnsubscribed);
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

        if (!findUser.Success)
        {
            await SendStatusAsync(Errors.FromSearch(findUser));
            return;
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
            await SendStatusAsync(Errors.FromSearch(findUser));
            return;
        }

        if (Context.Channel is not ITextChannel channel)
        {
            await SendStatusAsync(Errors.InvalidChannel);
            return;
        }

        // Every message of the follow would fail to send, don't add it
        if (!BotCanSendIn(channel.Id))
        {
            await SendStatusAsync(Warnings.BotCannotSend(channel.Mention));
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
            await SendStatusAsync(Errors.Internal);
            return;
        }

        if (!searchResult.Success)
        {
            await SendStatusAsync(Errors.FromSearch(searchResult));
            return;
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
            await SendStatusAsync(Errors.FromSearch(search));
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
            await SendStatusAsync(Errors.NoMoreResults);
            return;
        }

        var embed = ModrinthEmbedBuilder.GetMoreResultsEmbed(search.Payload.SearchResponse.Hits, query);

        await FollowupAsync(embed: embed.Build(),
            components: ModrinthComponentBuilder.GetResultSearchButton(search.Payload.SearchResponse.Hits).Build());
    }
}
