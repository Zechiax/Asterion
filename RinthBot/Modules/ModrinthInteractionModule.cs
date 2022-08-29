using Discord;
using Discord.Interactions;
using RinthBot.Attributes;
using RinthBot.Services;
using Modrinth.RestClient.Models;
using RinthBot.ComponentBuilders;

namespace RinthBot.Modules;

public class ModrinthInteractionModule : InteractionModuleBase
{
        public DataService DataService { get; set; } = null!;
        public ModrinthService ModrinthService { get; set; } = null!;

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

                if (guild is {UpdateChannel: null})
                {
                        await FollowupAsync(
                                $":warning: You didn't set default update channel, updates for projects subscribed from search will be send to default channel, set it through the [/modrinth set-update-channel](https://zechiax.gitbook.io/rinthbot/commands/set-update-chanel) command :warning:");
                }
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