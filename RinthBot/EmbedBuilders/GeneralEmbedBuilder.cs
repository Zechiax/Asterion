using Discord;

namespace RinthBot.EmbedBuilders;

public static class GeneralEmbedBuilder
{
    public static EmbedBuilder GetChangeChannelEmbed(string projectId, bool tooManyChannels = false)
    {
        var embed = new EmbedBuilder()
        {
            Title = "Change update channel",
            Description = "The current update channel for this project is per default set to this channel, but you can change it here\n\n" + (tooManyChannels
                ? $"You can find almost all your channels in the list, you have so many that I can't show them all, if you want to change to a channel that isn't here, use `/change-channel project_id:{projectId} [New Channel]`"
                : "You'll find here all your channels, you can later use the `/change-channel project_id:test new-channel:` command to change the update channel")
        };

        return embed;
    }
}