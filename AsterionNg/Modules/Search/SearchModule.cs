using AsterionNg.Assets;
using AsterionNg.Common;
using AsterionNg.Search;
using AsterionNg.Search.Providers;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace AsterionNg.Modules.Search;

[Group("search", "Searches for a project.")]
public class SearchModule(
    ProjectSearchService searchService,
    IStringLocalizerFactory factory,
    IGuildCultureProvider? cultureProvider)
    : ModuleBase(factory, cultureProvider)
{
    [SlashCommand("modrinth", "Searches for a project on Modrinth.")]
    public async Task SearchModrinthAsync(string query)
    {
        var results = await searchService.SearchProviderAsync(SearchProvider.Modrinth.ToString(), query);

        var embed = new EmbedBuilder()
            .WithTitle(L["Search.Results"])
            .WithDescription(string.Join("\n", results.Select(r => $"[{r.Name}]({r.ProjectUrl})")))
            .WithColor(Colors.Primary)
            .Build();

        await RespondAsync(embed: embed);
    }
}