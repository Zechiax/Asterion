using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.AutocompleteHandlers;

/// <summary>
///     Suggests the follows of the guild, the value is the follow ID
/// </summary>
public class FollowAutocompletionHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var permission = await FollowAutocompletion.CheckManageSubsAsync(context, services);
        if (permission is not null)
            return permission.Value;

        var followService = services.GetRequiredService<IFollowService>();
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        var follows = await followService.GetFollowsAsync(context.Guild.Id);

        var results = follows
            .Where(f => f.TargetName.Contains(userInput, StringComparison.InvariantCultureIgnoreCase)
                        || f.TargetId.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Select(f => new AutocompleteResult($"{f.TargetName} ({f.TargetType})".Truncate(100), f.Id));
        
        return AutocompletionResult.FromSuccess(results.Take(25));
    }
}

/// <summary>
///     Suggests the projects of the follow selected in the "follow" option. For the "ignore" command only
///     projects that are not ignored, for "unignore" only ignored ones.
/// </summary>
public class FollowProjectAutocompletionHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var permission = await FollowAutocompletion.CheckManageSubsAsync(context, services);
        if (permission is not null)
            return permission.Value;

        var followOption = autocompleteInteraction.Data.Options.FirstOrDefault(o => o.Name == "follow");
        if (followOption?.Value is null || !int.TryParse(followOption.Value.ToString(), out var followId))
            return AutocompletionResult.FromSuccess();

        var followService = services.GetRequiredService<IFollowService>();
        var follow = await followService.GetFollowAsync(context.Guild.Id, followId);
        if (follow is null)
            return AutocompletionResult.FromSuccess();

        bool? ignoredFilter = parameter.Command.Name switch
        {
            "ignore" => false,
            "unignore" => true,
            _ => null
        };

        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        var results = follow.Projects
            .Where(fp => ignoredFilter is null || fp.Ignored == ignoredFilter)
            .Select(fp => new AutocompleteResult($"{fp.ProjectId} - {fp.Project.Title}".Truncate(100), fp.ProjectId))
            .Where(r => r.Name.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(r => r.Name);

        return AutocompletionResult.FromSuccess(results.Take(25));
    }
}

internal static class FollowAutocompletion
{
    /// <summary>
    ///     Only administrators and users with the manage role may see the follows.
    ///     Returns an error result if the user may not, null if they may.
    /// </summary>
    public static async Task<AutocompletionResult?> CheckManageSubsAsync(IInteractionContext context,
        IServiceProvider services)
    {
        if (context.User is not IGuildUser guildUser)
            return AutocompletionResult.FromError(
                PreconditionResult.FromError("Command must be used in a guild channel."));

        if (guildUser.GuildPermissions.Administrator)
            return null;

        var manageRoleId = await services.GetRequiredService<IDataService>().GetManageRoleIdAsync(context.Guild.Id);
        if (manageRoleId.HasValue && guildUser.RoleIds.Contains(manageRoleId.Value))
            return null;

        // The error won't show to user, but to autocompletion handler
        return AutocompletionResult.FromError(
            PreconditionResult.FromError(
                "For this command the user needs administrator permission or manage role check"));
    }
}
