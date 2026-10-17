using Asterion.Attributes;
using Asterion.AutocompleteHandlers;
using Asterion.Common;
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

/// <summary>
///     Quick way to follow a creator, the follows are managed in <c>/manage</c>
/// </summary>
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
        [Summary("username", "Username, ID or link of the Modrinth user")] [MaxLength(60)]
        string username,
        [Summary("channel", "Channel for the messages, this channel if not set")] [AnnouncementChannelPrecondition]
        IGuildChannel? channel = null,
        [Summary("events", "Which events to send messages for")]
        FollowEventsChoice events = FollowEventsChoice.All,
        [Summary("new_project_channel", "Channel for new project messages, the channel above if not set")]
        [AnnouncementChannelPrecondition]
        IGuildChannel? newProjectChannel = null)
    {
        await DeferAsync();

        var textChannel = channel as ITextChannel ?? Context.Channel as ITextChannel;
        if (textChannel is null)
        {
            await FollowupAsync(Loc("follow-channel-invalid"));
            return;
        }

        var (search, result) = await Subscriptions.FollowUserAsync(modrinthService, followService, Context.Guild.Id,
            username, textChannel.Id, ToFollowEvents(events), newProjectChannel?.Id);
        if (result is null)
        {
            await FollowupWithSearchResultErrorAsync(search);
            return;
        }

        await FollowupAsync(FollowEmbedBuilder.DescribeAddResult(result, search.Payload.User.Username,
            textChannel.Mention, LocalizationService, CommandCultureInfo));

        if (result.Status != AddFollowStatus.Added)
            return;

        await WarnIfCannotSendAsync(textChannel);
        if (newProjectChannel is ITextChannel newProjectTextChannel && newProjectTextChannel.Id != textChannel.Id)
            await WarnIfCannotSendAsync(newProjectTextChannel);
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
