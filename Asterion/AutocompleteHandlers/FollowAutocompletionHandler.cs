using Asterion.Database.Models;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.AutocompleteHandlers;

/// <summary>
///     Suggests the followed users of the guild, the value is the Modrinth user ID
/// </summary>
public class FollowAutocompletionHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var permission = await ManageSubsAutocompletion.CheckAsync(context, services);
        if (permission is not null)
            return permission.Value;

        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;
        var follows = await services.GetRequiredService<IFollowService>().GetFollowsAsync(context.Guild.Id);

        var results = follows
            .Where(f => f.TargetType == FollowTargetType.User)
            .Where(f => f.TargetName.Contains(userInput, StringComparison.InvariantCultureIgnoreCase)
                        || f.TargetId.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(f => f.TargetName)
            .Select(f => new AutocompleteResult(f.TargetName.Truncate(100), f.TargetId));

        // max - 25 suggestions at a time (API limit)
        return AutocompletionResult.FromSuccess(results.Take(25));
    }
}
