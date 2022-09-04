using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using RinthBot.Attributes;
using RinthBot.Services;
using Modrinth.RestClient.Models;
using RinthBot.ComponentBuilders;
using RinthBot.EmbedBuilders;
using RinthBot.Services.Modrinth;

namespace RinthBot.Modules;

[EnabledInDm(false)]
public class ModrinthInteractionModule : InteractionModuleBase
{
        public DataService DataService { get; set; } = null!;
        public ModrinthService ModrinthService { get; set; } = null!;
        public ILogger<ModrinthInteractionModule> Logger = null!;

        private const string RequestError = "Sorry, there was an error processing your request, try again later";

        private MessageComponent GetButtons(Project project, bool subEnabled = true)
        {
                var components = ModrinthModule.GetSubscribeButtons(Context.User.Id, Context.Guild.Id, project.Id, subEnabled)
                        .WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project));

                return components.Build();
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [ComponentInteraction("sub-project:*;*;*", runMode: RunMode.Async)]
        public async Task SubProject(string userId, string projectId, ulong guildId)
        {
                await DeferAsync();

                guildId = Context.Guild.Id;
                var channel = await Context.Guild.GetTextChannelAsync(Context.Channel.Id);
                
                var subscribed = await DataService.IsGuildSubscribedToProjectAsync(guildId, projectId);
                
                var project = await ModrinthService.GetProject(projectId);

                if (project == null)
                {
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }

                // Already subscribed
                if (subscribed)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Components = GetButtons(project, false);
                        });
                        await FollowupAsync("You're already subscribed to updates for this project", ephemeral: true);
                        return;
                }
                
                var latestVersion = await ModrinthService.GetProjectsLatestVersion(project);
                
                if (latestVersion == null)
                {
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Components = GetButtons(project, false);
                });
                
                await DataService.AddModrinthProjectToGuildAsync(guildId, project.Id, latestVersion.Id, channel.Id, project.Title);

                await FollowupAsync($"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** :white_check_mark: Updates will be send to this channel {channel.Mention}", ephemeral: true);

                var guild = await DataService.GetGuildByIdAsync(guildId);

                var guildChannels = await Context.Guild.GetTextChannelsAsync();
                
                var options = new SelectMenuBuilder()
                {
                        CustomId = $"project_channels_{project.Id}",
                        Placeholder = "Select update channel",
                        Options = (await GetSelectMenuChannelList(Context.Guild, guildChannels, Context.Channel as ITextChannel)).Take(25).ToList(),
                        MinValues = 1,
                        MaxValues = 1,
                };
                
                
                var component = new ComponentBuilder().WithSelectMenu(options);
                await FollowupAsync(
                        embed: GeneralEmbedBuilder.GetChangeChannelEmbed(project, guildChannels.Count > 25).Build(),
                        ephemeral: true, components: component.Build());
        }
        
        [ComponentInteraction("project_channels_*")]
        public async Task ChannelSelection(string projectId, string[] selectedChannels)
        {
                if (ulong.TryParse(selectedChannels.First(), out var channelId) == false)
                {       
                        Logger.LogError("Could not convert {Id} to ulong", selectedChannels.First());
                        return;
                }

                await DataService.ChangeModrinthEntryChannel(Context.Guild.Id, projectId, channelId);

                await DeferAsync();
        }

        /// <summary>
        /// Returns list of all channels for Select Menu to be used
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="guildChannels"></param>
        /// <param name="defaultChannel">Default selected option</param>
        /// <returns></returns>
        public async static Task<List<SelectMenuOptionBuilder>> GetSelectMenuChannelList(IGuild guild, IReadOnlyCollection<ITextChannel>? guildChannels = null, ITextChannel? defaultChannel = null)
        {
                var channels = new List<SelectMenuOptionBuilder>();

                guildChannels ??= await guild.GetTextChannelsAsync();

                if (defaultChannel is not null)
                {
                        channels.Add(new SelectMenuOptionBuilder($"{defaultChannel.Name} (here)", defaultChannel.Id.ToString(),
                                emote: Emoji.Parse(":hash:"),isDefault: true));
                }

                foreach (var channel in guildChannels)
                {
                        if (defaultChannel != null && channel.Id == defaultChannel.Id)
                        {
                                continue;
                        }
                        
                        channels.Add(new SelectMenuOptionBuilder(channel.Name, channel.Id.ToString(), emote: Emoji.Parse(":hash:")));
                }

                return channels;
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [ComponentInteraction("unsub-project:*;*;*", runMode: RunMode.Async)]
        public async Task UnsubProject(string userId, string projectId, ulong guildId)
        {
                await DeferAsync();
                
                guildId = Context.Guild.Id;
                
                var subscribed = await DataService.IsGuildSubscribedToProjectAsync(guildId, projectId);
                
                var project = await ModrinthService.GetProject(projectId);

                // BUG: If the project does not exists, it will always throw an error
                if (project == null)
                {
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }

                // Already unsubscribed
                if (subscribed == false)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Components = GetButtons(project);
                        });
                        await FollowupAsync("You're already unsubscribed from updates to this project", ephemeral: true);
                        return;
                }
                
                await DataService.RemoveModrinthProjectFromGuildAsync(guildId, projectId);

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Components = GetButtons(project);
                });
                
                await FollowupAsync($"Unsubscribed from updates for project ID **{projectId}** :white_check_mark:", ephemeral: true);
        }
}