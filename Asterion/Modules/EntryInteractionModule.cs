using Asterion.Common;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using System.Threading.Tasks;
using Asterion.AutocompleteHandlers;
using Asterion.Database.Models;

namespace Asterion.Modules
{
    public class EntryInteractionModule(ILocalizationService localizationService, IDataService dataService)
        : AsterionInteractionModuleBase(localizationService)
    {
        // Interaction command to display the entry info and provide a dropdown
        [SlashCommand("entry", "Displays entry information and allows selection of release type filters.")]
        public async Task ShowEntryAsync([Summary("project_id")] [Autocomplete(typeof(SubscribedIdAutocompletionHandler))] string projectId)
        {
            await DeferAsync();
            
            // Retrieve entry data using your custom connector
            var entry = await dataService.GetModrinthEntryAsync(Context.Guild.Id, projectId);
            if (entry == null)
            {
                await RespondAsync("Entry not found.");
                return;
            }

            // Create embed with entry details
            var embed = new EmbedBuilder()
                .WithTitle("Modrinth Entry Information")
                .AddField("Project ID", entry.ProjectId)
                .AddField("Array ID", entry.ArrayId)
                .AddField("Custom Update Channel", entry.CustomUpdateChannel?.ToString() ?? "None")
                .AddField("Custom Ping Role", entry.CustomPingRole?.ToString() ?? "None")
                .AddField("Release Filter", entry.ReleaseFilter.ToString())
                .AddField("Created", TimestampTag.FromDateTime(entry.Created))
                .WithColor(Color.Blue)
                .Build();

            // Create a SelectMenu for ReleaseType filter
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"release_filter:{entry.EntryId}") // Unique ID to handle interactions
                .WithPlaceholder("Select release type(s)")
                .WithMinValues(1)
                .WithMaxValues(3)
                .AddOption("Alpha", ((int)ReleaseType.Alpha).ToString(), "Include Alpha releases",
                    isDefault: entry.ReleaseFilter.HasFlag(ReleaseType.Alpha))
                .AddOption("Beta", ((int)ReleaseType.Beta).ToString(), "Include Beta releases",
                    isDefault: entry.ReleaseFilter.HasFlag(ReleaseType.Beta))
                .AddOption("Release", ((int)ReleaseType.Release).ToString(), "Include Release",
                    isDefault: entry.ReleaseFilter.HasFlag(ReleaseType.Release));

            var component = new ComponentBuilder()
                .WithSelectMenu(selectMenu)
                .Build();

            // Send the embed with the dropdown menu
            await FollowupAsync(embed: embed, components: component);
        }

        // Handle the dropdown interaction
        [ComponentInteraction("release_filter:*")]
        public async Task HandleReleaseFilterSelectionAsync(string entryId, string[] selectedValues)
        {
            // Parse the entry ID
            // if (!ulong.TryParse(entryId, out var parsedEntryId))
            // {
            //     await RespondAsync("Invalid entry ID.");
            //     return;
            // }
            //
            // // Retrieve the entry
            // var entry = await _modrinthEntryService.GetEntryByIdAsync(parsedEntryId);
            // if (entry == null)
            // {
            //     await RespondAsync("Entry not found.");
            //     return;
            // }
            //
            // // Convert selectedValues back into the ReleaseType enum
            // ReleaseType? newReleaseFilter = null;
            // foreach (var value in selectedValues)
            // {
            //     if (int.TryParse(value, out var releaseTypeValue))
            //     {
            //         newReleaseFilter |= (ReleaseType)releaseTypeValue;
            //     }
            // }
            //
            // // Update the entry with the new filter (handle updating it in your data source here)
            // entry.ReleaseFilter = newReleaseFilter;
            // await _modrinthEntryService.UpdateEntryAsync(entry); // Update via your custom connector
            //
            // // Respond with the updated information
            // await RespondAsync($"Release filter updated to: {entry.ReleaseFilter}");
        }
    }
}
