using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Asterion.Common;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using System.Threading.Tasks;
using Asterion.Attributes;
using Asterion.AutocompleteHandlers;
using Asterion.Database.Models;
using Modrinth;

namespace Asterion.Modules
{
    [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
    [DoManageSubsRoleCheck(Group = "ManageSubs")]
    public class EntryInteractionModule(
        ILocalizationService localizationService,
        IDataService dataService,
        IModrinthClient modrinthClient)
        : AsterionInteractionModuleBase(localizationService)
    {
        public static Embed CreateModrinthEntryEmbed(ModrinthEntry entry, ReleaseType releaseFilter)
        {
            return new EmbedBuilder()
                .WithTitle("Modrinth Entry Information")
                .WithDescription("Here is the information about the Modrinth entry:")
                .AddField("📂 Project ID", $"`{entry.ProjectId}`", false)
                .AddField("📢 Custom Update Channel",
                    entry.CustomUpdateChannel.HasValue
                        ? MentionUtils.MentionChannel(entry.CustomUpdateChannel.Value)
                        : "None",
                    true)
                .AddField("📜 Custom Ping Role",
                    entry.CustomPingRole.HasValue ? MentionUtils.MentionRole(entry.CustomPingRole.Value) : "None",
                    false)
                .AddField("🔖 Release Filter", releaseFilter.ToString(), true)
                .AddField("🔍 Loader Filter",
                    entry.LoaderFilter == null ? "None" : string.Join(", ", entry.LoaderFilter), true)
                .AddField("🕒 Created", TimestampTag.FromDateTime(entry.Created).ToString(), false)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .Build();
        }

        private static SelectMenuBuilder CreateReleaseFilterComponent(string projectId, ReleaseType releaseFilter)
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

            return selectMenu;
        }

        private static SelectMenuBuilder CreateLoaderFilterComponent(string projectId, string[] supportedLoaders,
            string[]? currentLoaderFilter)
        {
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"loader_filter:{projectId}")
                .WithPlaceholder("Select loader(s) to filter - currently ALL")
                .WithMinValues(0)
                .WithMaxValues(supportedLoaders.Length);

            // Add options for each supported loader
            foreach (var loader in supportedLoaders)
            {
                selectMenu.AddOption(loader, loader, $"Include {loader} loader",
                    isDefault: currentLoaderFilter != null && currentLoaderFilter.Contains(loader));
            }

            return selectMenu;
        }

        // Interaction command to display the entry info and provide a dropdown
        [SlashCommand("entry", "Displays entry information and allows selection of release type filters.")]
        public async Task ShowEntryAsync(
            [Summary("project_id")] [Autocomplete(typeof(SubscribedIdAutocompletionHandler))]
            string projectId)
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

            var components = new ComponentBuilder();
            // Create a SelectMenu for ReleaseType filter
            var releaseFilterComponent = CreateReleaseFilterComponent(projectId, entry.ReleaseFilter);
            components.WithSelectMenu(releaseFilterComponent, 0);

            // Create a SelectMenu for Loader filter
            var project = await modrinthClient.Project.GetAsync(projectId);
            var supportedLoaders = project.Loaders;
            var loaderFilterComponent = CreateLoaderFilterComponent(projectId, supportedLoaders, entry.LoaderFilter);
            components.WithSelectMenu(loaderFilterComponent, 1);

            // Send the embed with the dropdown menu
            await FollowupAsync(embed: embed, components: components.Build());
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
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = new ComponentBuilder()
                    .WithSelectMenu(CreateReleaseFilterComponent(projectId, newReleaseFilter)).Build();
            });

            // Optionally, acknowledge the selection change in a temporary message
            await FollowupAsync("Release filter updated successfully!", ephemeral: true);
        }

        [ComponentInteraction("loader_filter:*")]
        public async Task HandleLoaderFilterSelectionAsync(string projectId, string[] selectedValues)
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

            // Convert selectedValues back into the Loader filter array
            string[]? newLoaderFilter = null;
            if (selectedValues.Length > 0)
            {
                newLoaderFilter = selectedValues;
            }

            // Update the loader filter in the database
            await dataService.SetLoaderFilterAsync(entry.EntryId, newLoaderFilter);
            entry.LoaderFilter = newLoaderFilter;

            // Use the static method to generate the updated embed
            var updatedEmbed = CreateModrinthEntryEmbed(entry, entry.ReleaseFilter);

            // TODO: Maybe parse the supported loaders from the selected project instead of fetching it again
            var supportedLoaders = (await modrinthClient.Project.GetAsync(projectId)).Loaders;

            // Update the original message with the new embed
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = updatedEmbed;
                msg.Components = new ComponentBuilder()
                    .WithSelectMenu(CreateLoaderFilterComponent(projectId, supportedLoaders, entry.LoaderFilter))
                    .Build();
            });

            // Optionally, acknowledge the selection change in a temporary message
            await FollowupAsync("Loader filter updated successfully!", ephemeral: true);
        }
    }
}