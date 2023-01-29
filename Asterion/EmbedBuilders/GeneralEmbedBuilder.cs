using Discord;
using Modrinth.Models;

namespace Asterion.EmbedBuilders;

public static class GeneralEmbedBuilder
{
    /// <summary>
    /// Created embed with information about changing 
    /// </summary>
    /// <param name="project">Project for which to change the update channel</param>
    /// <param name="tooManyChannels">Notify the user when the embed can't contain all channels</param>
    /// <returns></returns>
    public static EmbedBuilder GetChangeChannelEmbed(Project project, bool tooManyChannels = false)
    {
        var embed = new EmbedBuilder()
        {
            Title = $"Change update channel for {project.Title}",
            Description = "The current update channel for this project is per default set to this channel, but you can change it here\n\n" + (tooManyChannels
                ? $"You can find almost all your channels in the list, you have so many that I can't show them all, if you want to change to a channel that isn't here, use `/change-channel project_id:{project.Id} [New Channel]`"
                : "You'll find here all your channels, you can later use the `/change-channel project_id:test new-channel:` command to change the update channel")
        };

        return embed;
    }
}