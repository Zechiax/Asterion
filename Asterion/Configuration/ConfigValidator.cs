using Microsoft.Extensions.Configuration;

namespace Asterion.Configuration;

public static class ConfigValidator
{
    public static IReadOnlyList<string> Validate(IConfiguration config, bool isDebug)
    {
        var errors = new List<string>();

        var token = config[ConfigKeys.Token];
        if (string.IsNullOrWhiteSpace(token) || token == ConfigKeys.TokenPlaceholder)
            errors.Add($"'{ConfigKeys.Token}' is not set, put your Discord bot token there");

        if (isDebug && (!ulong.TryParse(config[ConfigKeys.TestGuild], out var guildId) || guildId == 0))
            errors.Add($"'{ConfigKeys.TestGuild}' must be a valid guild ID in Debug builds");

        var testChannel = config[ConfigKeys.TestChannel];
        if (!string.IsNullOrEmpty(testChannel) && !ulong.TryParse(testChannel, out _))
            errors.Add($"'{ConfigKeys.TestChannel}' must be a number");

        return errors;
    }
}
