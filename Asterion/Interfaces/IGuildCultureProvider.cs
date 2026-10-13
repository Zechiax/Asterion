using System.Globalization;
using Discord;

namespace Asterion.Interfaces;

public interface IGuildCultureProvider
{
    Task<CultureInfo> GetCultureAsync(IDiscordInteraction interaction);
}
