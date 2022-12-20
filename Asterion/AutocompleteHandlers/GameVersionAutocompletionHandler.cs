using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Modrinth.RestClient.Models.Enums;

namespace Asterion.AutocompleteHandlers;

public class GameVersionAutocompletionHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var modrinth = services.GetRequiredService<ModrinthService>();

        var versions = await modrinth.GetGameVersions();

        if (versions is null)
        {
            return AutocompletionResult.FromError(PreconditionResult.FromError("Could not get game version list"));
        }

        var userInput = autocompleteInteraction.Data.Current.Value.ToString();

        IEnumerable<AutocompleteResult> results;
        
        // If no user input, show the latest 25 major versions
        if (string.IsNullOrEmpty(userInput))
        {
            results = versions.Where(x => x.VersionType == GameVersionType.Release).OrderByDescending(x => x.Date)
                .Select(x => new AutocompleteResult(x.Version, x.Version));
        }
        else
        {
            results = versions.Where(x => x.Version.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
                .Select(x =>
                    new AutocompleteResult(
                        x.Version, x.Version));
            // Let's filter results based on user's input
        }

        return AutocompletionResult.FromSuccess(results.Take(25));
    }
}