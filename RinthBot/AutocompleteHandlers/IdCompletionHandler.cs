using System.Collections;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RinthBot.Services;

namespace RinthBot.AutocompleteHandlers;

public class IdCompletionHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var user = await context.Guild.GetUserAsync(context.User.Id);
        
        // TODO: Make this show to people who have role for managing subscribed projects
        // Only show list of subscribed projects to administrators
        if (user.GuildPermissions.Administrator == false)
        {
            return AutocompletionResult.FromSuccess();
        }
        
        var data = services.GetRequiredService<DataService>();
        var userInput = (context.Interaction as SocketAutocompleteInteraction)?.Data.Current.Value.ToString();
        var projects = await data.GetAllGuildsSubscribedProjectsAsync(context.Guild.Id);

        if (projects is null)
        {
            // If we return error, the user can't unsubscribe any project, this will display no results for the user
            return AutocompletionResult.FromSuccess();
        }
        
        var results = projects.Select(project => new AutocompleteResult(project.ProjectId, project.ProjectId))
            .Where(x => userInput != null && x.Name.Contains(userInput, StringComparison.InvariantCultureIgnoreCase));

        // max - 25 suggestions at a time (API limit)
        return AutocompletionResult.FromSuccess(results.Take(25));
    }
}