using System.Globalization;
using Discord;

namespace Asterion.Interfaces;

public interface IGuildCultureProvider
{
    Task<CultureInfo> GetCultureAsync(IDiscordInteraction interaction);

    /// <summary>Forgets the cached language of the guild, call it after changing the language</summary>
    void Invalidate(ulong guildId);
}
