using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Humanizer;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.AutocompleteHandlers;

public class SubscribedIdAutocompletionHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        // Only show list of subscribed projects to administrators or to users who have the manage subs role
        var permission = await ManageSubsAutocompletion.CheckAsync(context, services);
        if (permission is not null)
            return permission.Value;

        var data = services.GetRequiredService<IDataService>();
        var userInput = autocompleteInteraction.Data.Current.Value.ToString();
        var projects = await data.GetAllGuildsSubscribedProjectsAsync(context.Guild.Id);

        if (projects is null)
            // If we return error, the user can't unsubscribe any project, this will display no results for the user
            return AutocompletionResult.FromSuccess();

        var results = projects.Select(project =>
                new AutocompleteResult(
                    $"{project.ProjectId} - {project.Project.Title}"
                        .Truncate(100), // Truncate because 100 seems like Discord API's limit for autocomplete name
                    project.ProjectId))
            // Let's filter results based on user's input
            .Where(x => userInput != null && x.Name.Contains(userInput, StringComparison.InvariantCultureIgnoreCase));

        // max - 25 suggestions at a time (API limit)
        return AutocompletionResult.FromSuccess(results.Take(25));
    }
}