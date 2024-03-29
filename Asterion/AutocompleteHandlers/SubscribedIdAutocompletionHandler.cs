﻿using Asterion.Interfaces;
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
        var data = services.GetRequiredService<IDataService>();

        if (context.User is not IGuildUser guildUser)
            return AutocompletionResult.FromError(
                PreconditionResult.FromError("Command must be used in a guild channel."));

        var manageRoleId = await data.GetManageRoleIdAsync(context.Guild.Id);

        // Only show list of subscribed projects to administrators or to users who have the manage subs role 
        if (guildUser.GuildPermissions.Administrator == false &&
            !(manageRoleId.HasValue && guildUser.RoleIds.Contains(manageRoleId.Value)))
            // The error won't show to user, but to autocompletion handler
            return AutocompletionResult.FromError(
                PreconditionResult.FromError(
                    "For this command the user needs administrator permission or manage role check"));

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