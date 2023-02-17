using System.Text;
using Asterion.Attributes;
using Asterion.AutocompleteHandlers;
using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Asterion.Extensions;

// ReSharper disable MemberCanBePrivate.Global

namespace Asterion.Modules;

[EnabledInDm(false)]
[RequireContext(ContextType.Guild)]
// ReSharper disable once ClassNeverInstantiated.Global
public class ModrinthModule : InteractionModuleBase<SocketInteractionContext>
{
        private readonly IDataService _dataService;
        private readonly ModrinthService _modrinthService;
        private readonly InteractiveService _interactive;
        private readonly DiscordSocketClient _client;
        private readonly ILogger<ModrinthModule> _logger;

        public ModrinthModule(IServiceProvider serviceProvider)
        {
                _dataService = serviceProvider.GetRequiredService<IDataService>();
                _modrinthService = serviceProvider.GetRequiredService<ModrinthService>();
                _interactive = serviceProvider.GetRequiredService<InteractiveService>();
                _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
                _logger = serviceProvider.GetRequiredService<ILogger<ModrinthModule>>();
        }

        public ButtonBuilder GetSubscribeButtons(string projectId, bool subEnabled = true)
        {
                return ModrinthComponentBuilder.GetSubscribeButtons(Context.User.Id, projectId, subEnabled);
        }

        [SlashCommand("user", "Finds information about user, search by ID or username")]
        public async Task FindUser([Summary("Query", "ID or username")][MaxLength(60)] string query)
        {
                await DeferAsync();
                _logger.LogDebug("Search for user '{Query}", query);
                var searchResult = await _modrinthService.FindUser(query);
                _logger.LogDebug("Search status: {SearchStatus}", searchResult.SearchStatus);
                
                switch (searchResult.SearchStatus)
                {
                        case SearchStatus.ApiDown:
                                await FollowupAsync("Modrinth API is probably down, please try again later");
                                return;
                        case SearchStatus.NoResult:
                                await FollowupAsync($"No result for query '{query}'");
                                return;
                        case SearchStatus.UnknownError:
                                await FollowupAsync("Unknown error, please try again later");
                                return;
                        case SearchStatus.FoundById:
                                break;
                        case SearchStatus.FoundBySearch:
                                break;
                        default:
                                throw new ArgumentOutOfRangeException();
                }

                var userDto = searchResult.Payload;

                var embed = ModrinthEmbedBuilder.GetUserEmbed(searchResult);

                var components =
                        new ComponentBuilder().WithButton(ModrinthComponentBuilder.GetUserLinkButton(userDto.User));
                
                await FollowupAsync(embed: embed.Build(), components: components.Build());
        }

        [SlashCommand("search", "Search Projects (by slug, ID or search) and gives you info about the first response")]
        public async Task SearchProject([Summary("Query", "Query, ID or slug")][MaxLength(60)] string query)
        {
                await DeferAsync();
                _logger.LogDebug("Search for query '{Query}'", query);
                var searchResult = await _modrinthService.FindProject(query);
                _logger.LogDebug("Search status: {SearchStatus}", searchResult.SearchStatus);
                switch (searchResult.SearchStatus)
                {
                        case SearchStatus.ApiDown:
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        x.Content = "Modrinth API is probably down, please try again later";
                                });
                                return;
                        case SearchStatus.NoResult:
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        x.Content = $"No result for query '{query}'";
                                });
                                return;
                        case SearchStatus.UnknownError:
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        x.Content = "Unknown error, please try again later";
                                });
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

                var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

                if (guild is null)
                {
                        // Try again later
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "Internal error, please try again later";
                        });
                        return;
                }

                var subscribedToProject = await _dataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, project.Id);
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Embed = ModrinthEmbedBuilder.GetProjectEmbed(searchResult, team).Build();
                        var components = new ComponentBuilder();

                        if ((bool) guild.GuildSettings.ShowSubscribeButton!)
                        {
                                components.WithButton(GetSubscribeButtons(project.Id,
                                        !subscribedToProject));
                        }

                        components.WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project))
                                .WithButton(ModrinthComponentBuilder.GetUserToViewButton(Context.User.Id, team.GetOwner()?.User.Id, project.Id))
                                .WithButton(ModrinthComponentBuilder.ViewMoreSearchResults(projectDto.SearchResponse, query), 1);
                                
                        x.Components = components.Build();
                });
        }

        [MessageCommand("Find project on Modrinth")]
        public async Task SearchOnModrinth(IMessage msg)
        {
                if (string.IsNullOrEmpty(msg.Content))
                {
                        await RespondAsync("Message contains no content to search for", ephemeral: true);
                        return;
                }

                await SearchProject(msg.Content);
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("subscribe", "Add a Modrinth project to your watched list")]
        public async Task Subscribe(string projectId, SocketTextChannel? customChannel = null)
        {
                await DeferAsync();
                var project = await _modrinthService.GetProject(projectId);

                var channel = customChannel ?? Context.Guild.GetTextChannel(Context.Channel.Id);
                
                if (project == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error processing your request, check if the ID is correct or try again later";
                        });
                        return;
                }
                
                var versions = await _modrinthService.GetVersionListAsync(project.Id);
                if (versions == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error processing your request, check if the ID is correct or try again later";
                        });
                        return;
                }
                
                // Get last version ID
                var lastVersion = versions.OrderByDescending(x => x.DatePublished).First().Id;

                await _dataService.AddModrinthProjectToGuildAsync(Context.Guild.Id, project.Id, lastVersion, channel.Id, project.Title);
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $@"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** updates will be send to channel {channel.Mention} :white_check_mark:";
                });
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("change-channel", "Change channel for one of your subscribed projects")]
        public async Task ChangeChannel(
                [Summary("project_id"), Autocomplete(typeof(SubscribedIdAutocompletionHandler))] string projectId,
                SocketTextChannel newChannel)
        {
                await DeferAsync();
                
                var subscribed = await _dataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, projectId);

                if (!subscribed)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"You're now subscribed to project ID {projectId}";
                        });
                        return;
                }

                var success = await _dataService.ChangeModrinthEntryChannel(Context.Guild.Id, projectId, newChannel.Id);

                if (success)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"New updates for project {projectId} will be send to channel {newChannel.Mention} :white_check_mark:";
                        });
                }
                else
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"Something went wrong while changing the update channel, please try again later";
                        });
                }
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("unsubscribe", "Remove Modrinth project from your watched list")]
        public async Task Unsubscribe([Summary("project_id"), Autocomplete(typeof(SubscribedIdAutocompletionHandler))]string projectId)
        {
                await DeferAsync();
                var removed = await _dataService.RemoveModrinthProjectFromGuildAsync(Context.Guild.Id, projectId);

                if (removed == false)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"Project with ID {projectId} is not subscribed";
                        });
                        return;
                }

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $"Unsubscribed from updates for project ID **{projectId}** :white_check_mark:";
                });
        }
        
        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("unsubscribe-all", "Removes all subscribed projects")]
        public async Task UnsubscribeAll()
        {
                var builder = new ComponentBuilder()
                        .WithButton("Yes", "remove-all-data-yes", ButtonStyle.Danger);

                await RespondAsync("**Are you sure you want to remove all projects?** \n\n**Click the button if Yes, if not, just wait**", components: builder.Build());
                
                // Wait for a user to press the button
                var result = await _interactive.NextMessageComponentAsync(x => x.Data.Type == ComponentType.Button 
                                                                              && x.Data.CustomId == "remove-all-data-yes" 
                                                                              && Context.User.Id == x.User.Id 
                                                                              && x.Message.Interaction.Id == Context.Interaction.Id, 
                        timeout: TimeSpan.FromSeconds(15));


                if (result.IsSuccess)
                {
                        // Acknowledge the interaction
                        await result.Value!.DeferAsync();

                        foreach (var versions in (await _dataService.GetAllGuildsSubscribedProjectsAsync(Context.Guild.Id))!)
                        {
                                await _dataService.RemoveModrinthProjectFromGuildAsync(Context.Guild.Id, versions.ProjectId);
                        }
                }

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = result.IsSuccess ? "All data cleared" : "Action cancelled";
                        x.Components = new ComponentBuilder().Build(); // No components
                        x.AllowedMentions = AllowedMentions.None;
                });
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("list", "Lists all your subscribed projects")]
        public async Task ListSubscribed()
        {
                await DeferAsync();
                var list = (await _dataService.GetAllGuildsSubscribedProjectsAsync(Context.Guild.Id))!.OrderBy(x => x.Project.Title).ToList();

                if (list.Count == 0)
                {
                        await FollowupAsync("You are not subscribed to any projects");
                        return;
                }
                
                var sb = new StringBuilder();
                
                sb.AppendLine("Title | Id | Channel");
                foreach (var project in list)
                {
                        // Find custom channel for this project
                        var customChannel = list.Find(x => x.ProjectId == project.ProjectId)?.CustomUpdateChannel;

                        // Get update channel for this project
                        var channel = customChannel != null
                                ? _client.GetGuild(Context.Guild.Id).GetTextChannel((ulong) customChannel)
                                : null;

                        sb.AppendLine($@"> **{project.Project.Title}** | {project.ProjectId} {
                                (channel != null ? 
                                        $"| {channel.Mention}" : "| *not set*")}");
                }
                
                // Split the string into multiple messages as Discord has a limit of 2000 characters per message
                // We should split by line as we don't want to split a line in half
                var first = true;
                for (var i = 0; i < sb.Length; i += 2000)
                {
                        var length = Math.Min(2000, sb.Length - i);
                        var message = sb.ToString(i, length);
                        if (first)
                        {
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        x.Content = message;
                                });
                                first = false;
                        }
                        else
                        {
                                await FollowupAsync(message);
                        }
                }
        }

        [SlashCommand("latest-release", "Gets the latest release of project")]
        public async Task LatestRelease(string slugOrId, MessageStyle style = MessageStyle.Full)
        {
                await DeferAsync();
                var project = await _modrinthService.GetProject(slugOrId);

                if (project == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "Project not found";
                        });
                        return;
                }

                var latestVersion = await _modrinthService.GetProjectsLatestVersion(project);
                
                if (latestVersion == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "Error";
                        });
                        return;
                }

                var team = await _modrinthService.GetProjectsTeamMembersAsync(project.Id);

                var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(style, project, latestVersion, team);
                
                var buttons =
                        new ComponentBuilder().WithButton(
                                ModrinthComponentBuilder.GetVersionUrlButton(project, latestVersion));
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Embed = embed.Build();
                        x.Components = buttons.Build();
                });
        }

        [RequireOwner]
        [SlashCommand("force-update", "Forces check for updates")]
        public async Task ForceUpdate()
        {
                await DeferAsync(true);
                var ok = _modrinthService.ForceUpdate();
                await FollowupAsync($"Forced Update request: {ok}");
        }
}