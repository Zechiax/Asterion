using Discord;
using Discord.Net;

namespace Asterion.Common;

/// <summary>
///     Sends a Components V2 message to an interaction, in whichever way the interaction can still be answered
/// </summary>
public static class InteractionResponder
{
    public static async Task SendAsync(IDiscordInteraction interaction, MessageComponent components, bool ephemeral)
    {
        var flags = ephemeral ? MessageFlags.ComponentsV2 | MessageFlags.Ephemeral : MessageFlags.ComponentsV2;

        if (!interaction.HasResponded)
        {
            await interaction.RespondAsync(components: components, ephemeral: ephemeral, flags: flags);
            return;
        }

        // A deferred command still shows "thinking…" until something replaces it. Otherwise the original is
        // a result that was already sent, or the message of a clicked button (e.g. a panel), which must stay
        var original = await TryGetOriginalAsync(interaction);
        if (original?.Flags is { } originalFlags && originalFlags.HasFlag(MessageFlags.Loading))
        {
            // An ephemeral placeholder can't be made public, so it's replaced in both cases
            if (!ephemeral || originalFlags.HasFlag(MessageFlags.Ephemeral))
            {
                await interaction.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = components;
                    msg.Flags = MessageFlags.ComponentsV2;
                });
                return;
            }

            // The placeholder is public, but only the user should see the message
            await interaction.DeleteOriginalResponseAsync();
        }

        await interaction.FollowupAsync(components: components, ephemeral: ephemeral, flags: flags);
    }

    private static async Task<IUserMessage?> TryGetOriginalAsync(IDiscordInteraction interaction)
    {
        try
        {
            return await interaction.GetOriginalResponseAsync();
        }
        catch (HttpException)
        {
            // Deleted, or the interaction only has follow-ups
            return null;
        }
    }
}
