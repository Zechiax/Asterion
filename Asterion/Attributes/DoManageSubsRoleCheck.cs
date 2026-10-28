using Asterion.Common;
using Asterion.Interfaces;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Asterion.Attributes;

public class DoManageSubsRoleCheck : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo commandInfo, IServiceProvider services)
    {
        // Guild only precondition
        if (context.User is not IGuildUser guildUser)
            return LocalizedPreconditionResult.FromError(Errors.GuildOnly);

        var guildService = services.GetRequiredService<IGuildService>();

        // Get manage role id
        var roleId = await guildService.GetManageRoleIdAsync(context.Guild.Id);

        // Check if it has been set
        if (roleId.HasValue == false) return LocalizedPreconditionResult.FromError(Errors.ManagePermission);

        // If set and the user have this role, precondition is successful
        if (guildUser.RoleIds.Contains(roleId.Value)) return PreconditionResult.FromSuccess();

        return LocalizedPreconditionResult.FromError(Errors.ManagePermission);
    }
}