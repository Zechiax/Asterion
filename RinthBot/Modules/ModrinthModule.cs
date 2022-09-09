using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RinthBot.Attributes;
using RinthBot.EmbedBuilders;
using Fergun.Interactive;
using Microsoft.Extensions.Logging;
using RinthBot.AutocompleteHandlers;
using RinthBot.ComponentBuilders;
using RinthBot.Interfaces;
using RinthBot.Services.Modrinth;

// ReSharper disable MemberCanBePrivate.Global

namespace RinthBot.Modules;

[EnabledInDm(false)]
[RequireContext(ContextType.Guild)]
// ReSharper disable once ClassNeverInstantiated.Global
public class ModrinthModule : InteractionModuleBase<SocketInteractionContext>
{
        public IDataService DataService { get; set; } = null!;
        public ModrinthService ModrinthService { get; set; } = null!;
        public InteractiveService Interactive { get; set; } = null!;
        public DiscordSocketClient Client { get; set; } = null!;
        public ILogger<ModrinthModule> Logger { get; set; } = null!;

        public ComponentBuilder GetSubscribeButtons(string projectId,
                bool subEnabled = true)
        {
                return GetSubscribeButtons(Context.User.Id, Context.Guild.Id, projectId, subEnabled);
        }

        public static ComponentBuilder GetSubscribeButtons(ulong userId, ulong guildId, string projectId, bool subEnabled = true)
        {
                var buttons = new ComponentBuilder()
                        .WithButton(
                                subEnabled ? "Subscribe" : "Unsubscribe",
                                // Write unsub when the subEnabled is false
                                customId: $"{(subEnabled ? null : "un")}sub-project:{userId};{projectId};{guildId}",
                                style: subEnabled ? ButtonStyle.Success : ButtonStyle.Danger,
                                emote: subEnabled ? Emoji.Parse(":bell:") : Emoji.Parse(":no_bell:"));

                return buttons;
        }

        [SlashCommand("search", "Search Projects (by slug, ID or search) and gives you info about the first response")]
        public async Task SearchProject(string query) // TODO: in Discord.NET >3.8 use MaxLength of 60
        {
                await DeferAsync();
                Logger.LogDebug("Search for query '{Query}'", query);
                var searchResult = await ModrinthService.FindProject(query);
                Logger.LogDebug("Search status: {SearchStatus}", searchResult.SearchStatus);
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

                var project = searchResult.Payload!;

                var team = await ModrinthService.GetProjectsTeamMembersAsync(project.Id);
                
                var subscribedToProject = await DataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, project.Id);
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Embed = ModrinthEmbedBuilder.GetProjectEmbed(project, team).Build();
                        x.Components = GetSubscribeButtons(project.Id, !subscribedToProject)
                                .WithButton(ModrinthComponentBuilder.GetProjectLinkButton(project))
                                .Build();
                });
        }

        [MessageCommand("Search on Modrinth")]
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
                var project = await ModrinthService.GetProject(projectId);

                var channel = customChannel ?? Context.Guild.GetTextChannel(Context.Channel.Id);
                
                if (project == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error processing your request, check if the ID is correct or try again later";
                        });
                        return;
                }
                
                var versions = await ModrinthService.GetVersionListAsync(project.Id);
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

                await DataService.AddModrinthProjectToGuildAsync(Context.Guild.Id, project.Id, lastVersion, channel.Id, project.Title);
                
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
                
                var subscribed = await DataService.IsGuildSubscribedToProjectAsync(Context.Guild.Id, projectId);

                if (!subscribed)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"You're now subscribed to project ID {projectId}";
                        });
                        return;
                }

                var success = await DataService.ChangeModrinthEntryChannel(Context.Guild.Id, projectId, newChannel.Id);

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
                var removed = await DataService.RemoveModrinthProjectFromGuildAsync(Context.Guild.Id, projectId);

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
                var result = await Interactive.NextMessageComponentAsync(x => x.Data.Type == ComponentType.Button 
                                                                              && x.Data.CustomId == "remove-all-data-yes" 
                                                                              && Context.User.Id == x.User.Id 
                                                                              && x.Message.Interaction.Id == Context.Interaction.Id, 
                        timeout: TimeSpan.FromSeconds(15));


                if (result.IsSuccess)
                {
                        // Acknowledge the interaction
                        await result.Value!.DeferAsync();

                        foreach (var versions in (await DataService.GetAllGuildsSubscribedProjectsAsync(Context.Guild.Id))!)
                        {
                                await DataService.RemoveModrinthProjectFromGuildAsync(Context.Guild.Id, versions.ProjectId);
                        }
                }

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = result.IsSuccess ? $"All data cleared" : $"Action cancelled";
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
                var list = (await DataService.GetAllGuildsSubscribedProjectsAsync(Context.Guild.Id))!.ToList();

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
                                ? Client.GetGuild(Context.Guild.Id).GetTextChannel((ulong) customChannel)
                                : null;

                        sb.AppendLine($@"> **{project.Project.Title}** | {project.ProjectId} {
                                (channel != null ? 
                                        $"| {channel.Mention}" : "| *not set*")}");
                }
                
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = sb.ToString();
                });
        }

        [SlashCommand("latest-release", "Gets the latest release of project")]
        public async Task LatestRelease(string slugOrId)
        {
                await DeferAsync();
                var project = await ModrinthService.GetProject(slugOrId);

                if (project == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "Project not found";
                        });
                        return;
                }

                var latestVersion = await ModrinthService.GetProjectsLatestVersion(project);
                
                if (latestVersion == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "Error";
                        });
                        return;
                }

                var team = await ModrinthService.GetProjectsTeamMembersAsync(project.Id);

                var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(project, latestVersion, team)
                        .WithTitle($"{project.Title} | Latest version");
                
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
                await DeferAsync();
                var ok = ModrinthService.ForceUpdate();

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $"Forced Update request: {ok}";
                        x.Flags = MessageFlags.Ephemeral;
                });
        }
}