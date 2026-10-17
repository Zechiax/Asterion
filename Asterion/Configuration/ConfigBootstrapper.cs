using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Asterion.Configuration;

public enum ConfigBootstrapResult
{
    Existing,

    Migrated,

    TemplateCreated
}

public static class ConfigBootstrapper
{
    public static ConfigBootstrapResult EnsureConfigFile(string directory, ILogger? logger = null)
    {
        var tomlPath = Path.Combine(directory, ConfigKeys.FileName);
        var jsonPath = Path.Combine(directory, ConfigKeys.LegacyFileName);

        if (File.Exists(tomlPath))
            return ConfigBootstrapResult.Existing;

        if (File.Exists(jsonPath))
        {
            File.WriteAllText(tomlPath, MigrateJson(File.ReadAllText(jsonPath)), new UTF8Encoding(false));

            var migratedPath = Path.Combine(directory, ConfigKeys.LegacyMigratedFileName);
            File.Move(jsonPath, migratedPath, true);

            logger?.LogWarning("Migrated {Json} to {Toml}, the original was renamed to {Migrated}",
                jsonPath, tomlPath, migratedPath);
            return ConfigBootstrapResult.Migrated;
        }

        File.WriteAllText(tomlPath, BuildToml(ConfigKeys.TokenPlaceholder, 0, 0, ""), new UTF8Encoding(false));
        logger?.LogWarning("No configuration found, created a template at {Path}", tomlPath);
        return ConfigBootstrapResult.TemplateCreated;
    }

    public static string MigrateJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var values = doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        string Get(string key) => values.GetValueOrDefault(key) ?? "";
        // Old placeholder values like "<insert-test-guild-id>" are not numbers, they become 0
        ulong GetId(string key) => ulong.TryParse(Get(key), out var id) ? id : 0;

        return BuildToml(Get("token"), GetId("testGuild"), GetId("testChannel"), Get("topGgToken"));
    }

    private static string BuildToml(string token, ulong testGuild, ulong testChannel, string topGgToken) => $"""
        # Asterion configuration
        #
        # The bot will NOT start until the values below are filled in.
        # Any value can also be set by an environment variable, e.g. {ConfigKeys.EnvPrefix}TOKEN for '{ConfigKeys.Token}'.

        # Discord bot token (https://discord.com/developers/applications -> Bot -> Token). Required.
        {ConfigKeys.Token} = {Quote(token)}

        # Guild (server) ID used to register commands instantly while running a Debug build.
        # Required for Debug builds only, ignored in Release.
        {ConfigKeys.TestGuild} = {testGuild}

        # Channel ID used for testing. Optional.
        {ConfigKeys.TestChannel} = {testChannel}

        # top.gg API token for publishing server stats. Optional, leave empty to disable.
        {ConfigKeys.TopGgToken} = {Quote(topGgToken)}
        """;

    // JSON string escaping is also valid for TOML basic strings
    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
