using Asterion.Attributes;
using Asterion.AutocompleteHandlers;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;

namespace Asterion.Modules;

public enum FollowEventsChoice
{
    [ChoiceDisplay("Project updates and new projects")]
    All,

    [ChoiceDisplay("Only project updates")]
    Updates,

    [ChoiceDisplay("Only new projects")]
    NewProjects
}

[Group("follow", "Follow Modrinth creators and get updates for all of their projects")]
[RequireContext(ContextType.Guild)]
[RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
[DoManageSubsRoleCheck(Group = "ManageSubs")]
// ReSharper disable once ClassNeverInstantiated.Global
public class FollowModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider,
    IFollowService followService,
    ModrinthService modrinthService)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    [SlashCommand("user", "Follow a Modrinth user and get updates for all of their projects")]
    public async Task FollowUser(
        [Summary("username", "Username or ID of the Modrinth user")] [MaxLength(60)]
        string username,
        [Summary("channel", "Channel for the messages, this channel if not set")] [AnnouncementChannelPrecondition]
        IGuildChannel? channel = null,
        [Summary("events", "Which events to send messages for")]
        FollowEventsChoice events = FollowEventsChoice.All)
    {
        await DeferAsync();

        var searchResult = await modrinthService.FindUser(username);
        if (!searchResult.Success)
        {
            await FollowupWithSearchResultErrorAsync(searchResult);
            return;
        }

        var textChannel = channel as ITextChannel ?? Context.Channel as ITextChannel;
        if (textChannel is null)
        {
            await FollowupAsync(Loc("follow-channel-invalid"));
            return;
        }

        var user = searchResult.Payload.User;
        var result = await followService.AddFollowAsync(Context.Guild.Id, FollowTargetType.User, user.Id,
            user.Username, textChannel.Id, ToFollowEvents(events));

        await FollowupAsync(FollowEmbedBuilder.DescribeAddResult(result, user.Username, textChannel.Mention,
            LocalizationService, CommandCultureInfo));

        if (result.Status == AddFollowStatus.Added)
            await WarnIfCannotSendAsync(textChannel);
    }

    [SlashCommand("remove", "Stop following a creator")]
    public async Task RemoveFollow(
        [Summary("follow", "The followed creator")] [Autocomplete(typeof(FollowAutocompletionHandler))]
        int follow)
    {
        await DeferAsync();

        var existing = await followService.GetFollowAsync(Context.Guild.Id, follow);
        if (existing is null || !await followService.RemoveFollowAsync(Context.Guild.Id, follow))
        {
            await FollowupAsync(Loc("follow-not-found"));
            return;
        }

        await FollowupAsync(Loc("follow-removed", new { name = existing.TargetName }));
    }

    [SlashCommand("ignore", "Stop sending updates for one project of a followed creator")]
    public Task Ignore(
        [Summary("follow", "The followed creator")] [Autocomplete(typeof(FollowAutocompletionHandler))]
        int follow,
        [Summary("project", "The project to ignore")] [Autocomplete(typeof(FollowProjectAutocompletionHandler))]
        string project)
    {
        return SetIgnoredAsync(follow, project, true);
    }

    [SlashCommand("unignore", "Send updates again for an ignored project of a followed creator")]
    public Task Unignore(
        [Summary("follow", "The followed creator")] [Autocomplete(typeof(FollowAutocompletionHandler))]
        int follow,
        [Summary("project", "The project to stop ignoring")]
        [Autocomplete(typeof(FollowProjectAutocompletionHandler))]
        string project)
    {
        return SetIgnoredAsync(follow, project, false);
    }

    [SlashCommand("settings", "Show and change the settings of a followed creator")]
    public async Task Settings(
        [Summary("follow", "The followed creator")] [Autocomplete(typeof(FollowAutocompletionHandler))]
        int follow,
        [Summary("channel", "New channel for the messages")] [AnnouncementChannelPrecondition]
        IGuildChannel? channel = null,
        [Summary("ping_role", "Role to ping for update messages")]
        IRole? pingRole = null,
        [Summary("new_project_ping_role", "Role to ping for new project messages, the update ping role if not set")]
        IRole? newProjectPingRole = null,
        [Summary("reset_ping_roles", "Remove both ping roles of this follow")]
        bool resetPingRoles = false)
    {
        await DeferAsync();

        var changed = channel is not null || pingRole is not null || newProjectPingRole is not null || resetPingRoles;

        var updated = changed
            ? await followService.UpdateSettingsAsync(Context.Guild.Id, follow, f =>
            {
                if (channel is not null) f.CustomUpdateChannel = channel.Id;

                if (resetPingRoles)
                {
                    f.CustomPingRole = null;
                    f.NewProjectPingRole = null;
                }

                if (pingRole is not null) f.CustomPingRole = pingRole.Id;
                if (newProjectPingRole is not null) f.NewProjectPingRole = newProjectPingRole.Id;
            })
            : await followService.GetFollowAsync(Context.Guild.Id, follow);

        if (updated is null)
        {
            await FollowupAsync(Loc("follow-not-found"));
            return;
        }

        var panel = await FollowInteractionModule.BuildSettingsPanelAsync(followService, updated, Context.User.Id, ListInteractionModule.NoList,
            LocalizationService, CommandCultureInfo);

        await FollowupAsync(components: panel, flags: MessageFlags.ComponentsV2);
    }

    private async Task SetIgnoredAsync(int followId, string projectId, bool ignored)
    {
        await DeferAsync();

        var follow = await followService.GetFollowAsync(Context.Guild.Id, followId);
        if (follow is null)
        {
            await FollowupAsync(Loc("follow-not-found"));
            return;
        }

        var followProject = follow.Projects.FirstOrDefault(fp => fp.ProjectId == projectId);
        if (followProject is null ||
            !await followService.SetIgnoredAsync(Context.Guild.Id, followId, projectId, ignored))
        {
            await FollowupAsync(Loc("follow-project-not-found", new { name = follow.TargetName, project = projectId }));
            return;
        }

        var args = new { name = follow.TargetName, project = followProject.Project.Title ?? projectId };
        await FollowupAsync(Loc(ignored ? "follow-project-ignored" : "follow-project-unignored", args));
    }

    private async Task WarnIfCannotSendAsync(ITextChannel channel)
    {
        var botUser = await channel.Guild.GetCurrentUserAsync();
        if (botUser.GetPermissions(channel).SendMessages)
            return;

        await FollowupAsync(Loc("follow-no-send-permission", new { channel = channel.Mention }), ephemeral: true);
    }

    private string Loc(string key)
    {
        return LocalizationService.Get(key, CommandCultureInfo);
    }

    private string Loc(string key, object args)
    {
        return LocalizationService.Get(key, CommandCultureInfo, args);
    }

    private static FollowEvents ToFollowEvents(FollowEventsChoice choice)
    {
        return choice switch
        {
            FollowEventsChoice.Updates => FollowEvents.ProjectUpdate,
            FollowEventsChoice.NewProjects => FollowEvents.NewProject,
            _ => FollowEvents.ProjectUpdate | FollowEvents.NewProject
        };
    }
}
