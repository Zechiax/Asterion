using Asterion.Attributes;
using Asterion.AutocompleteHandlers;
using Asterion.Common;
using Asterion.ComponentBuilders;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;

namespace Asterion.Modules;

/// <summary>
///     Quick way to stop following a creator, counterpart of <see cref="FollowModule" />
/// </summary>
[Group("unfollow", "Stop following Modrinth creators")]
[RequireContext(ContextType.Guild)]
[RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
[DoManageSubsRoleCheck(Group = "ManageSubs")]
// ReSharper disable once ClassNeverInstantiated.Global
public class UnfollowModule(
    ILocalizationService localizationService,
    IGuildCultureProvider cultureProvider,
    IFollowService followService,
    ModrinthService modrinthService)
    : AsterionInteractionModuleBase(localizationService, cultureProvider)
{
    // "{id}:{ownerId}:{followId}", only the user who ran /unfollow can confirm
    public const string ConfirmButtonId = "unfollow-cmd-confirm";
    public const string CancelButtonId = "unfollow-cmd-cancel";

    [SlashCommand("user", "Stop following a Modrinth user")]
    public async Task UnfollowUser(
        [Summary("user", "Username, ID or link of the followed Modrinth user")]
        [Autocomplete(typeof(FollowAutocompletionHandler))]
        string user)
    {
        await DeferAsync();

        var lookup = await Subscriptions.FindFollowAsync(modrinthService, followService, Context.Guild.Id, user);
        if (lookup.Found is not { } follow)
        {
            await SendStatusAsync(lookup.Error ?? Warnings.NotFollowing);
            return;
        }

        var question = LocalizationService.Get("follow-remove-confirm-question", CommandCultureInfo,
            new { name = Format.Sanitize(follow.TargetName) });

        await FollowupAsync(components: SettingsPanelBuilder.BuildMessage(question, SettingsPanelBuilder.FollowAccent,
                new ButtonBuilder(LocalizationService.Get("button-confirm", CommandCultureInfo),
                    $"{ConfirmButtonId}:{Context.User.Id}:{follow.Id}", ButtonStyle.Danger),
                new ButtonBuilder(LocalizationService.Get("button-cancel", CommandCultureInfo),
                    $"{CancelButtonId}:{Context.User.Id}:{follow.Id}", ButtonStyle.Secondary)),
            flags: MessageFlags.ComponentsV2);
    }

    [DoUserCheck]
    [ComponentInteraction(ConfirmButtonId + ":*:*", ignoreGroupNames: true)]
    public async Task HandleConfirmAsync(ulong ownerId, int followId)
    {
        await DeferAsync();

        var removed = await followService.RemoveFollowAsync(Context.Guild.Id, followId);
        var text = removed is null
            ? LocalizationService.Get("follow-not-found", CommandCultureInfo)
            : LocalizationService.Get("follow-removed", CommandCultureInfo,
                new { name = Format.Sanitize(removed.TargetName) });

        await ModifyOriginalResponseAsync(msg =>
            msg.Components = SettingsPanelBuilder.BuildMessage(text, SettingsPanelBuilder.FollowAccent));
    }

    [DoUserCheck]
    [ComponentInteraction(CancelButtonId + ":*:*", ignoreGroupNames: true)]
    public async Task HandleCancelAsync(ulong ownerId, int followId)
    {
        await DeferAsync();
        await ModifyOriginalResponseAsync(msg => msg.Components = SettingsPanelBuilder.BuildMessage(
            LocalizationService.Get("removal-cancelled", CommandCultureInfo), SettingsPanelBuilder.FollowAccent));
    }
}
