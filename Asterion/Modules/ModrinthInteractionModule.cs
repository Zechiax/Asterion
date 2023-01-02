using Asterion.Attributes;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth.RestClient.Models;
using Asterion.Extensions;

namespace Asterion.Modules;

[EnabledInDm(false)]
public class ModrinthInteractionModule : InteractionModuleBase
{
        private readonly IDataService _dataService;
        private readonly ModrinthService _modrinthService;
        private readonly ILogger<ModrinthInteractionModule> _logger;

        private const string RequestError = "Sorry, there was an error processing your request, try again later";

        public ModrinthInteractionModule(IServiceProvider serviceProvider)
        {
                _dataService = serviceProvider.GetRequiredService<IDataService>();
                _modrinthService = serviceProvider.GetRequiredService<ModrinthService>();
                _logger = serviceProvider.GetRequiredService<ILogger<ModrinthInteractionModule>>();
        }

        private ComponentBuilder GetButtons(Project project, GuildSettings guildSettings, bool subEnabled = true, IEnumerable<TeamMember>? team = null)
        {
                var components = new ComponentBuilder();

                if ((bool) guildSettings.ShowSubscribeButton!)
                {
                        components.WithButton( ModrinthComponentBuilder.GetSubscribeButtons(Context.User.Id, project.Id, subEnabled));
                }
                
                components.WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project))
                .WithButton(ModrinthComponentBuilder.GetUserToViewButton(Context.User.Id,
                        team.GetOwner()?.User.Id, project.Id));

                return components;
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [ComponentInteraction("sub-project:*;*", runMode: RunMode.Async)]
        public async Task SubProject(string userId, string projectId)
        {
                await DeferAsync();

                var guildId = Context.Guild.Id;
                var channel = await Context.Guild.GetTextChannelAsync(Context.Channel.Id);
                
                var subscribed = await _dataService.IsGuildSubscribedToProjectAsync(guildId, projectId);

                var project = await _modrinthService.GetProject(projectId);

                if (project == null)
                {
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }
                
                var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);
                var guild = await _dataService.GetGuildByIdAsync(guildId);

                if (guild is null)
                {
                        // Try again later
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }

                // Already subscribed
                if (subscribed)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Components = GetButtons(project, guild.Settings, false, team).Build();
                        });
                        await FollowupAsync("You're already subscribed to updates for this project", ephemeral: true);
                        return;
                }
                
                var latestVersion = await _modrinthService.GetProjectsLatestVersion(project);
                
                if (latestVersion == null)
                {
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Components = GetButtons(project, guild.Settings, false, team).Build();
                });
                
                await _dataService.AddModrinthProjectToGuildAsync(guildId, project.Id, latestVersion.Id, channel.Id, project.Title);

                await FollowupAsync($"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** :white_check_mark: Updates will be send to this channel {channel.Mention}", ephemeral: true);
                

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
                        _logger.LogError("Could not convert {Id} to ulong", selectedChannels.First());
                        return;
                }

                await _dataService.ChangeModrinthEntryChannel(Context.Guild.Id, projectId, channelId);

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
        [ComponentInteraction("unsub-project:*;*", runMode: RunMode.Async)]
        public async Task UnsubProject(string userId, string projectId)
        {
                await DeferAsync();
                
                var guildId = Context.Guild.Id;
                
                var subscribed = await _dataService.IsGuildSubscribedToProjectAsync(guildId, projectId);
                
                var project = await _modrinthService.GetProject(projectId);
                var guild = await _dataService.GetGuildByIdAsync(guildId);
                
                if (guild is null)
                {
                        // Try again later
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }

                // BUG: If the project does not exists, it will always throw an error
                if (project == null)
                {
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }
                
                var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

                // Already unsubscribed
                if (subscribed == false)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Components = GetButtons(project, guild.Settings, team: team).Build();
                        });
                        await FollowupAsync("You're already unsubscribed from updates to this project", ephemeral: true);
                        return;
                }
                
                await _dataService.RemoveModrinthProjectFromGuildAsync(guildId, projectId);

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Components = GetButtons(project, guild.Settings, team: team).Build();
                });
                
                await FollowupAsync($"Unsubscribed from updates for project ID **{projectId}** :white_check_mark:", ephemeral: true);
        }

        [ComponentInteraction("show-user:*;*;*", runMode: RunMode.Async)]
        public async Task ShowUser(ulong discordUserId, string modrinthUserId, string projectId)
        {
                await DeferAsync(ephemeral: true);

                var findUser = await _modrinthService.FindUser(modrinthUserId);

                switch (findUser.SearchStatus)
                {
                        case SearchStatus.ApiDown:
                                await FollowupAsync("Modrinth API is down, please try again later", ephemeral: true);
                                return;
                        case SearchStatus.NoResult:
                                await FollowupAsync("User does not exist", ephemeral: true);
                                return;
                        case SearchStatus.UnknownError:
                                await FollowupAsync("There was an unknown error, please try again later", ephemeral: true);
                                return;
                        case SearchStatus.FoundById:
                                break;
                        case SearchStatus.FoundBySearch:
                                break;
                        default:
                                throw new ArgumentOutOfRangeException();
                }

                await FollowupAsync(embed: ModrinthEmbedBuilder.GetUserEmbed(findUser).Build(),
                        components: new ComponentBuilder()
                                //.WithButton(ModrinthComponentBuilder.BackToProjectButton(discordUserId, projectId))
                                .WithButton(ModrinthComponentBuilder.GetUserLinkButton(findUser.Payload.User))
                                .Build(),
                        ephemeral: true);
        }
        
                [ComponentInteraction("back-project:*;*", runMode: RunMode.Async)]
        public async Task BackToProject(ulong discordUserId, string projectId)
        {
                //await DeferAsync();

                var searchResult = await _modrinthService.FindProject(projectId);
                var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

                if (guild is null)
                {
                        // Try again later
                        await FollowupAsync(RequestError, ephemeral: true);
                        return;
                }

                switch (searchResult.SearchStatus)
                {
                        case SearchStatus.ApiDown:
                                await FollowupAsync("Modrinth API is probably down, please try again later", ephemeral: true);
                                return;
                        case SearchStatus.NoResult:
                                await FollowupAsync("Project does not exist", ephemeral: true);
                                return;
                        case SearchStatus.UnknownError:
                                await FollowupAsync("Unknown error, please try again later", ephemeral: true);
                                return;
                        case SearchStatus.FoundById:
                                break;
                        case SearchStatus.FoundBySearch:
                                break;
                        default:
                                throw new ArgumentOutOfRangeException();
                }

                var projectDto = searchResult.Payload;
                var project = projectDto.Project;

                var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

                var subscribedToProject = await _dataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, project.Id);
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Embed = ModrinthEmbedBuilder.GetProjectEmbed(searchResult, team).Build();
                        x.Components = GetButtons(project, guild.Settings, !subscribedToProject, team)
                                .Build();
                });
        }

        [ComponentInteraction("view-project-from-search:*", runMode: RunMode.Async)]
        public async Task ViewProjectFromSearch(string projectId)
        {
                await DeferAsync();

                var search = await _modrinthService.FindProject(projectId);

                if (search.Success == false)
                {
                        await FollowupAsync("Couldn't find this project", ephemeral: true);
                        return; 
                }

                await BackToProject(Context.User.Id, projectId);
        }

        [ComponentInteraction("more-results:|*|", runMode: RunMode.Async)]
        public async Task MoreResults(string query)
        {
                await DeferAsync();
                query = query.Replace('_', ' ');
                
                var search = await _modrinthService.FindProject(query);

                if (search.Payload.SearchResponse is null)
                {
                        await FollowupAsync("Could not find any more projects", ephemeral: true);
                        return;
                }

                var embed = ModrinthEmbedBuilder.GetMoreResultsEmbed(search.Payload.SearchResponse.Hits, query);

                await FollowupAsync(embed: embed.Build(), components: ModrinthComponentBuilder.GetResultSearchButton(search.Payload.SearchResponse.Hits).Build());
        }
}