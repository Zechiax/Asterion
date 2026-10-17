namespace Asterion.Configuration;

public static class ConfigKeys
{
    public const string FileName = "config.toml";
    public const string LegacyFileName = "config.json";
    public const string LegacyMigratedFileName = "config.json.migrated";

    // Environment variables with this prefix override the file, e.g. ASTERION_TOKEN -> token
    public const string EnvPrefix = "ASTERION_";

    public const string TokenPlaceholder = "YOUR_BOT_TOKEN_HERE";

    public const string Token = "token";
    public const string TestGuild = "test_guild";
    public const string TestChannel = "test_channel";
    public const string TopGgToken = "top_gg_token";
}
