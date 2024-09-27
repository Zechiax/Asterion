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
        public static Embed CreateModrinthEntryEmbed(ModrinthEntry entry, ReleaseType releaseFilter)
        {
            return new EmbedBuilder()
                .WithTitle("Modrinth Entry Information")
                .WithDescription("Here is the information about the Modrinth entry:")
                .AddField("📂 Project ID", $"`{entry.ProjectId}`", false)
                .AddField("📢 Custom Update Channel", 
                    entry.CustomUpdateChannel.HasValue ? MentionUtils.MentionChannel(entry.CustomUpdateChannel.Value) : "None", 
                    true)
                .AddField("📜 Custom Ping Role", 
                    entry.CustomPingRole.HasValue ? MentionUtils.MentionRole(entry.CustomPingRole.Value) : "None", 
                    false)
                .AddField("🔖 Release Filter", releaseFilter.ToString(), true)
                .AddField("🕒 Created", TimestampTag.FromDateTime(entry.Created).ToString(), true)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .Build();
        }
        
        private static MessageComponent CreateReleaseFilterComponent(string projectId, ReleaseType releaseFilter)
        {
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"release_filter:{projectId}")
                .WithPlaceholder("Select release type(s)")
                .WithMinValues(1)
                .WithMaxValues(3)
                .AddOption("Alpha", ((int)ReleaseType.Alpha).ToString(), "Include Alpha releases",
                    isDefault: releaseFilter.HasFlag(ReleaseType.Alpha))
                .AddOption("Beta", ((int)ReleaseType.Beta).ToString(), "Include Beta releases",
                    isDefault: releaseFilter.HasFlag(ReleaseType.Beta))
                .AddOption("Release", ((int)ReleaseType.Release).ToString(), "Include Release",
                    isDefault: releaseFilter.HasFlag(ReleaseType.Release));

            return new ComponentBuilder()
                .WithSelectMenu(selectMenu)
                .Build();
        }
        
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
            var embed = CreateModrinthEntryEmbed(entry, entry.ReleaseFilter);


            // Create a SelectMenu for ReleaseType filter
            var component = CreateReleaseFilterComponent(projectId, entry.ReleaseFilter);

            // Send the embed with the dropdown menu
            await FollowupAsync(embed: embed, components: component);
        }

        [ComponentInteraction("release_filter:*")]
        public async Task HandleReleaseFilterSelectionAsync(string projectId, string[] selectedValues)
        {
            // Defer the interaction to avoid timeout issues
            await DeferAsync(ephemeral: true);

            // Retrieve the entry using projectId
            var entry = await dataService.GetModrinthEntryAsync(Context.Guild.Id, projectId);
            if (entry == null)
            {
                await FollowupAsync("Entry not found.", ephemeral: true);
                return;
            }

            // Convert selectedValues back into the ReleaseType enum
            ReleaseType newReleaseFilter = 0;
            foreach (var value in selectedValues)
            {
                if (int.TryParse(value, out var releaseTypeValue))
                {
                    newReleaseFilter |= (ReleaseType)releaseTypeValue;
                }
            }

            if (newReleaseFilter == 0)
            {
                await FollowupAsync("Please select at least one release type.", ephemeral: true);
                return;
            }

            // Update the release filter in the database
            await dataService.SetReleaseFilterAsync(entry.EntryId, newReleaseFilter);

            // Use the static method to generate the updated embed
            var updatedEmbed = CreateModrinthEntryEmbed(entry, newReleaseFilter);

            // Update the original message with the new embed
            await ModifyOriginalResponseAsync(msg => {
                msg.Embed = updatedEmbed;
                msg.Components = CreateReleaseFilterComponent(projectId, newReleaseFilter);
            });

            // Optionally, acknowledge the selection change in a temporary message
            await FollowupAsync("Release filter updated successfully!", ephemeral: true);
        }


    }
}
