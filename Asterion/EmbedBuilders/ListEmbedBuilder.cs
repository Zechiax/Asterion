using Asterion.Database.Models;
using Discord;

namespace Asterion.EmbedBuilders;

public static class ListEmbedBuilder
{
    public static List<Embed> CreateListEmbed(IList<ModrinthEntry> entries)
    {
        var embeds = new List<Embed>();
        var embed = new EmbedBuilder
        {
            Title = "List of all your projects",
            Description = "Here you can find all the projects you are subscribed to"
        };

        var currentEntry = 0;

        var numberOfEmbeds = entries.Count / 25 + (entries.Count % 25 > 0 ? 1 : 0);

        for (var i = 0; i < numberOfEmbeds; i++)
        {
            // If i is 0, it's already initialized
            if (i > 0) embed = new EmbedBuilder();
            embed.Footer = new EmbedFooterBuilder
            {
                Text = $"Page {i + 1} of {numberOfEmbeds}"
            };

            for (var j = 0; j < DiscordEmbedConstants.MaxEmbedFieldCount; j++)
            {
                if (currentEntry >= entries.Count) break;

                var field = new EmbedFieldBuilder();

                var entry = entries[currentEntry];

                field.Name = $"{currentEntry + 1}. {entry.Project.Title} ({entry.Project.ProjectId})";
                field.Value =
                    $"In <#{entry.CustomUpdateChannel}> since {TimestampTag.FromDateTime(entry.Created, TimestampTagStyles.ShortDate)}";

                embed.AddField(field);

                currentEntry++;
            }

            embeds.Add(embed.Build());
        }

        return embeds;
    }
}