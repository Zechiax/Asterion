using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RinthBot.Attributes;
using RinthBot.EmbedBuilders;
using RinthBot.Services;
using Fergun.Interactive;
using Microsoft.Extensions.Logging;

// ReSharper disable MemberCanBePrivate.Global

namespace RinthBot.Modules;

[Group("modrinth", "Everything around Modrinth")]
// ReSharper disable once ClassNeverInstantiated.Global
public class ModrinthModule : InteractionModuleBase<SocketInteractionContext>
{
        public DataService DataService { get; set; } = null!;
        public ModrinthService ModrinthService { get; set; } = null!;
        public InteractiveService Interactive { get; set; } = null!;
        public DiscordSocketClient Client { get; set; } = null!;
        public ILogger<ModrinthModule> Logger { get; set; } = null!;

        [SlashCommand("search", "Search Projects (by slug, ID or search) and gives you info about the first response")]
        public async Task SearchProject(string query)
        {
                await DeferAsync();
                
                // First let's try searching by slug or ID
                // If the query contains space, it can't be slug or ID
                var project = query.Contains(' ') ? null : await ModrinthService.GetProject(query);

                // No results? Let's try normal search
                if (project == null)
                {
                        var searchResponse = await ModrinthService.SearchProjects(query);

                        // APIs could be down
                        if (searchResponse == null)
                        {
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        // TODO: Check modrinth status message
                                        x.Content = "Something went wrong";
                                });
                        }
                        // No search results
                        else if (searchResponse.TotalHits < 1)
                        {
                                if (project == null)
                                {
                                        await ModifyOriginalResponseAsync(x =>
                                        {
                                                x.Content = $"No search results for query '{query}'";
                                        });
                                        return;
                                }
                        }
                        
                        // Get info about the first search result
                        project = await ModrinthService.GetProject(searchResponse!.Hits[0].ProjectId);  
                        
                        // Something failed while getting the info
                        if (project == null)
                        {
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        x.Content = "There was an error with the request";
                                });
                                return;
                        }
                }

                var subscribedToProject = DataService.IsGuildSubscribedToProject(project, Context.Guild);
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Embed = ModrinthEmbedBuilder.GetProjectEmbed(project).Build();
                        x.Components = GetSubscribeButtons(project.Id, !subscribedToProject).Build();
                });
        }

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

        [DoAdminCheck]
        [SlashCommand("subscribe", "Add a Modrinth project to your watched list")]
        public async Task Subscribe(string projectId, SocketChannel? customChannel = null)
        {
                await DeferAsync();
                var project = await ModrinthService.GetProject(projectId);

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

                await DataService.AddWatchedProject(Context.Guild, project, lastVersion, customChannel);

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** :white_check_mark:";
                });
        }
        
        [DoAdminCheck]
        [SlashCommand("unsubscribe", "Remove Modrinth project from your watched list")]
        public async Task Unsubscribe(string projectId)
        {
                await DeferAsync();
                DataService.RemoveWatchedProject(Context.Guild, projectId);

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $"Unsubscribed from updates for project ID **{projectId}** :white_check_mark:";
                });
        }
        
        [DoAdminCheck]
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

                        foreach (var versions in DataService.GetGuildsSubscribedProjects(Context.Guild))
                        {
                                // TODO: Implement mass removal method in DataService
                                DataService.RemoveWatchedProject(Context.Guild, versions.ProjectId);
                        }
                }

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = result.IsSuccess ? $"All data cleared" : $"Action cancelled";
                        x.Components = new ComponentBuilder().Build(); // No components
                        x.AllowedMentions = AllowedMentions.None;
                });
        }

        [DoAdminCheck]
        [SlashCommand("list", "Lists all your subscribed projects")]
        public async Task ListSubscribed()
        {
                await DeferAsync();
                var list = DataService.ListProjects(Context.Guild).ToList();

                if (list.Count == 0)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "You have not subscribed to any projects";
                        });
                        return;
                }

                var ids = list.Select(x => x.ProjectId).ToList();

                var projects = await ModrinthService.GetMultipleProjects(ids);
                if (projects == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error processing your request";
                        });
                        return;
                }
                var sb = new StringBuilder();
                
                foreach (var project in projects)
                {
                        sb.Append($"> **{project.Title}** | {project.Id}\n");
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

                // TODO: Centralize error messages
                if (latestVersion == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "Error";
                        });
                        return;
                }

                var embed = ModrinthEmbedBuilder.VersionUpdateEmbed(project, latestVersion)
                        .WithTitle($"{project.Title} | Latest version");
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Embed = embed.Build();
                });
        }

        [DoAdminCheck]
        [SlashCommand("set-update-channel", "Sets the update channel")]
        public async Task SetUpdateChannel(SocketTextChannel channel)
        {
                await DeferAsync();
                DataService.SetUpdateChannel(Context.Guild, channel);

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $"Channel for updates set to {channel.Mention} :white_check_mark:";
                });
        }

        [DoAdminCheck]
        [SlashCommand("test-setup", "Checks your setup (also tries to send a message to test channel and remove it)")]
        public async Task SendTextMessage()
        {
                await DeferAsync();
                       
                var guildInfoDb = DataService.GetGuild(Context.Guild);

                if (guildInfoDb.UpdateChannel == null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $":no_entry_sign: You didn't setup your update channel";
                        });
                        return;
                }
                
                var channel = Client.GetGuild(guildInfoDb.Id).GetTextChannel((ulong)guildInfoDb.UpdateChannel!);

                try
                {
                        var msg = await channel.SendMessageAsync("Test message");

                        await msg.DeleteAsync();
                }
                // null reference exception will only be thrown when channel is null
                catch (NullReferenceException)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content =
                                        $":no_entry_sign: Looks like the current update channel doesn't exists, please set a different one";
                        });
                        return;
                }
                catch (Exception e)
                {
                        if (e.Message.Contains("Missing Access") || e.Message.Contains("Missing Permissions"))
                        {
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        x.Content =
                                                $":no_entry_sign: Looks like I don't have permission to send messages to {channel.Mention} channel, check my permissions for that channel and try again";
                                });
                        }
                        else
                        {
                                Logger.LogWarning("Test setup on server {Guild}: Unknown error: '{ExceptionMessage}'", Context.Guild.Id, e.Message);
                                await ModifyOriginalResponseAsync(x =>
                                {
                                        x.Content = $"Unknown error happened";
                                });
                        }

                        return;
                }

                var subs = DataService.GetGuildsSubscribedProjects(Context.Guild);
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $"Everything's in check :white_check_mark: {(subs.Any() ? null : "Now I recommend subscribing to some projects")}";
                });
        }

        [DoOwnerCheck]
        [SlashCommand("force-update", "Forces check for updates")]
        public async Task ForceUpdate()
        {
                await DeferAsync();
                var ok = ModrinthService.ForceUpdate();

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = $"Forced Update request: {ok}";
                });
        }
}

public class ModrinthInteractionModule : InteractionModuleBase
{
        public DataService DataService { get; set; } = null!;
        public ModrinthService ModrinthService { get; set; } = null!;

        [DoAdminCheck]
        [ComponentInteraction("sub-project:*;*;*", runMode: RunMode.Async)]
        public async Task SubProject(string userId, string projectId, ulong guildId)
        {
                await DeferAsync();

                var subscribed = DataService.IsGuildSubscribedToProject(projectId, guildId);

                if (subscribed)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Components = ModrinthModule
                                        .GetSubscribeButtons(Context.User.Id, Context.Guild.Id, projectId, false).Build();
                        });
                        await FollowupAsync("You're already subscribed to updates for this project", ephemeral: true);
                        return;
                }

                var project = await ModrinthService.GetProject(projectId);

                if (project == null)
                {
                        await FollowupAsync($"There was an error processing your request", ephemeral: true);
                        return;
                }

                var latestVersion = await ModrinthService.GetProjectsLatestVersion(project);
                
                if (latestVersion == null)
                {
                        await FollowupAsync($"There was an error processing your request", ephemeral: true);
                        return;
                }
                
                await ModifyOriginalResponseAsync(x =>
                {
                        x.Components = ModrinthModule
                                .GetSubscribeButtons(Context.User.Id, Context.Guild.Id, projectId, false).Build();
                });
                
                await DataService.AddWatchedProject(guildId, project, latestVersion.Id);

                await FollowupAsync($"Subscribed to updates for project **{project.Title}** with ID **{project.Id}** :white_check_mark:", ephemeral: true);
        }

        [DoAdminCheck]
        [ComponentInteraction("unsub-project:*;*;*", runMode: RunMode.Async)]
        public async Task UnsubProject(string userId, string projectId, ulong guildId)
        {
                await DeferAsync();
                
                var subscribed = DataService.IsGuildSubscribedToProject(projectId, guildId);

                if (subscribed == false)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Components = ModrinthModule
                                        .GetSubscribeButtons(Context.User.Id, Context.Guild.Id, projectId).Build();
                        });
                        await FollowupAsync("You're already unsubscribed from updates to this project", ephemeral: true);
                        return;
                }
                
                DataService.RemoveWatchedProject(guildId, projectId);

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Components = ModrinthModule
                                .GetSubscribeButtons(Context.User.Id, Context.Guild.Id, projectId).Build();
                });
                
                await FollowupAsync($"Unsubscribed from updates for project ID **{projectId}** :white_check_mark:", ephemeral: true);
        }
}

