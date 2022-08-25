using Discord;
using Discord.Interactions;
using RinthBot.Services;

namespace RinthBot.Modules;

public class GuildManagement : InteractionModuleBase<SocketInteractionContext>
{
        public DataService DataService { get; set; } = null!;
        
        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("set-manage-role", "Set's the role so that users with that role can manage subscription")]
        public async Task SetManageRole(IRole role)
        {
                await DeferAsync();
                var success = await DataService.SetManageRoleAsync(Context.Guild.Id, role.Id);

                if (success)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"Manage role set to role {role.Mention} :white_check_mark: Users with this role can now manage subscription";
                                x.Flags = MessageFlags.Ephemeral;
                                x.AllowedMentions = AllowedMentions.None;
                        });
                }
                else
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error while setting the manage role, please try again later";
                                x.Flags = MessageFlags.Ephemeral;
                                x.AllowedMentions = AllowedMentions.None;
                        });   
                }
        }

        [RequireUserPermission(GuildPermission.Administrator)]
        [SlashCommand("remove-manage-role",
                "Removes the management role")]
        public async Task RemoveManageRole()
        {
                await DeferAsync();

                var oldRole = await DataService.GetManageRoleIdAsync(Context.Guild.Id);

                if (oldRole.HasValue == false)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There is no manage role set";
                                x.Flags = MessageFlags.Ephemeral;
                        });
                        return;
                }

                var success = await DataService.SetManageRoleAsync(Context.Guild.Id, null);

                if (success)
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = $"Manage role has been removed";
                                x.Flags = MessageFlags.Ephemeral;
                                x.AllowedMentions = AllowedMentions.None;
                        });
                }
                else
                {
                        await ModifyOriginalResponseAsync(x =>
                        {
                                x.Content = "There was an error while removing the manage role, please try again later";
                                x.Flags = MessageFlags.Ephemeral;
                                x.AllowedMentions = AllowedMentions.None;
                        });  
                }
        }
}