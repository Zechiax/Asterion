﻿using System.Collections;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using RinthBot.Services;

namespace RinthBot.AutocompleteHandlers;

public class IdCompletionHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var data = services.GetRequiredService<DataService>();

        var projects = await data.GetAllGuildsSubscribedProjectsAsync(context.Guild.Id);

        if (projects is null)
        {
            // If we return error, the user can't unsubscribe any project, this will display no results for the user
            return AutocompletionResult.FromSuccess();
        }

        var results = projects.Select(project => new AutocompleteResult(project.ProjectId, project.ProjectId));

        // max - 25 suggestions at a time (API limit)
        return AutocompletionResult.FromSuccess(results.Take(25));
    }
}