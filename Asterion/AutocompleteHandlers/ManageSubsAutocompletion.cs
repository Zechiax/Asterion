using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.AutocompleteHandlers;

internal static class ManageSubsAutocompletion
{
    /// <summary>
    ///     Only administrators and users with the manage role may see the subscriptions and follows.
    ///     Returns an error result if the user may not, null if they may.
    /// </summary>
    public static async Task<AutocompletionResult?> CheckAsync(IInteractionContext context, IServiceProvider services)
    {
        if (context.User is not IGuildUser guildUser)
            return AutocompletionResult.FromError(
                PreconditionResult.FromError("Command must be used in a guild channel."));

        if (guildUser.GuildPermissions.Administrator)
            return null;

        var manageRoleId = await services.GetRequiredService<IGuildService>().GetManageRoleIdAsync(context.Guild.Id);
        if (manageRoleId.HasValue && guildUser.RoleIds.Contains(manageRoleId.Value))
            return null;

        // The error won't show to user, but to autocompletion handler
        return AutocompletionResult.FromError(
            PreconditionResult.FromError(
                "For this command the user needs administrator permission or manage role check"));
    }
}
