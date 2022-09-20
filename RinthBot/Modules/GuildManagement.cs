using System.Text;
using Discord;
using Discord.Interactions;
using RinthBot.Attributes;
using RinthBot.Database.Models;
using RinthBot.Interfaces;

namespace RinthBot.Modules;

[EnabledInDm(false)]
public class GuildManagement : InteractionModuleBase<SocketInteractionContext>
{
        private readonly IDataService _dataService;

        public GuildManagement(IDataService dataService)
        {
                _dataService = dataService;
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("set-manage-role", "Set's the role so that users with that role can manage subscription")]
        public async Task SetManageRole(IRole role)
        {
                await DeferAsync(true);
                var success = await _dataService.SetManageRoleAsync(Context.Guild.Id, role.Id);

                if (success)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"Manage role set to role {role.Mention} :white_check_mark: Users with this role can now manage subscription";
                                x.AllowedMentions = AllowedMentions.None;
                        });
                }
                else
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error while setting the manage role, please try again later";
                                x.AllowedMentions = AllowedMentions.None;
                        });   
                }
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("remove-manage-role",
                "Removes the management role")]
        public async Task RemoveManageRole()
        {
                await DeferAsync(true);

                var oldRole = await _dataService.GetManageRoleIdAsync(Context.Guild.Id);

                if (oldRole.HasValue == false)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There is no manage role set";
                                x.Flags = MessageFlags.Ephemeral;
                        });
                        return;
                }

                var success = await _dataService.SetManageRoleAsync(Context.Guild.Id, null);

                if (success)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"Manage role has been removed";
                                x.AllowedMentions = AllowedMentions.None;
                        });
                }
                else
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error while removing the manage role, please try again later";
                                x.AllowedMentions = AllowedMentions.None;
                        });  
                }
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("set-ping-role", "Sets the role, which will be notified every time a project gets an update")]
        public async Task SetPingRole(IRole role)
        {
                await DeferAsync(true);
                var success = await _dataService.SetPingRoleAsync(Context.Guild.Id, role.Id);

                if (success)
                {
                        await FollowupAsync(
                                $"Ping role set to role {role.Mention} :white_check_mark: This role will be notified with each update");
                }
                else
                {
                        await FollowupAsync("There was an error while setting the ping role, please try again later");
                }
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("message-style", "Sets the style of the update message")]
        public async Task SetMessageStyle(MessageStyle style)
        {
                await DeferAsync(ephemeral: true);

                var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

                if (guild is null)
                {
                        await FollowupAsync("Something went wrong, please try again later");
                        return;
                }

                guild.GuildSettings.MessageStyle = style;

                var success = await _dataService.UpdateGuildAsync(guild);

                if (!success)
                {
                        await FollowupAsync("There was an error while settings the message style, please try again later");
                        return;
                }
                
                await FollowupAsync($"Message style set to '{style}' :white_check_mark:");
        }

        [RequireUserPermission(GuildPermission.Administrator, Group = "ManageSubs")]
        [DoManageSubsRoleCheck(Group = "ManageSubs")]
        [SlashCommand("remove-ping-role", "Removes the ping role")]
        public async Task RemovePingRole()
        {
                await DeferAsync(true);
                var oldRole = await _dataService.GetPingRoleIdAsync(Context.Guild.Id);
                
                if (oldRole.HasValue == false)
                {
                        await FollowupAsync("There is no ping role set");
                        return;
                }
                
                var success = await _dataService.SetPingRoleAsync(Context.Guild.Id, null);

                if (success)
                {
                        await FollowupAsync(
                                "Ping role has been removed");
                }
                else
                {
                        await FollowupAsync("There was an error while removing the ping role, please try again later");
                }
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("info",
                "List basic information")]
        public async Task Stats()
        {
                await DeferAsync(ephemeral: true);

                var subscribedProjects = await _dataService.GetAllGuildsSubscribedProjectsAsync(Context.Guild.Id);
                var manageRoleId = await _dataService.GetManageRoleIdAsync(Context.Guild.Id);
                var pingRoleId = await _dataService.GetPingRoleIdAsync(Context.Guild.Id);

                var manageRole = manageRoleId is null ? null : Context.Guild.GetRole((ulong)manageRoleId);
                var pingRole = pingRoleId is null ? null : Context.Guild.GetRole((ulong) pingRoleId);

                if (subscribedProjects is null)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error while processing this request";
                                x.Flags = MessageFlags.Ephemeral;
                                x.AllowedMentions = AllowedMentions.None;
                        });  
                }

                var sb = new StringBuilder();

                sb.AppendLine(Format.Bold(Format.Underline($"Info for guild {Context.Guild.Name}")));
                sb.AppendLine();

                sb.AppendLine(Format.Bold("Ping role: ") + $"{(pingRole is null ? "Not set" : pingRole.Mention)}");
                sb.AppendLine(Format.Bold("Manage role: ") + $"{(manageRole is null ? "Not set" : manageRole.Mention)}");
                sb.AppendLine(Format.Bold("Number of subscribed projects: ") + subscribedProjects!.Count);

                await ModifyOriginalResponseAsync(x =>
                {
                        x.Content = sb.ToString();
                });
        }
}