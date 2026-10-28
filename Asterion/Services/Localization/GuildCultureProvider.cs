using System.Globalization;
using Asterion.Interfaces;
using Discord;
using Microsoft.Extensions.Caching.Memory;

namespace Asterion.Services.Localization;

public class GuildCultureProvider : IGuildCultureProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IGuildService _guildService;
    private readonly IMemoryCache _cache;

    public GuildCultureProvider(IGuildService guildService, IMemoryCache cache)
    {
        _guildService = guildService;
        _cache = cache;
    }

    public async Task<CultureInfo> GetCultureAsync(IDiscordInteraction interaction)
    {
        if (interaction.GuildId is { } guildId)
        {
            var language = await GetGuildLanguageAsync(guildId);
            if (!string.IsNullOrWhiteSpace(language) && TryCulture(language, out var chosen))
                return chosen;
        }

        // Discord's own locale — guild-wide first (responses are usually public),
        // then the invoking user's client language.
        if (TryCulture(interaction.GuildLocale, out var guildCulture)) return guildCulture;
        if (TryCulture(interaction.UserLocale, out var userCulture)) return userCulture;

        return CultureInfo.GetCultureInfo(FluentBundleStore.DefaultLocale);
    }

    public void Invalidate(ulong guildId)
    {
        _cache.Remove(CacheKey(guildId));
    }

    private static string CacheKey(ulong guildId) => $"guild-language-{guildId}";

    private Task<string?> GetGuildLanguageAsync(ulong guildId)
    {
        return _cache.GetOrCreateAsync(CacheKey(guildId), async entry =>
        {
            entry.SlidingExpiration = CacheDuration;
            var guild = await _guildService.GetGuildAsync(guildId);
            return guild?.GuildSettings.Language;
        });
    }

    private static bool TryCulture(string? name, out CultureInfo culture)
    {
        culture = CultureInfo.InvariantCulture;

        if (string.IsNullOrWhiteSpace(name)) return false;

        try
        {
            culture = CultureInfo.GetCultureInfo(name);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
