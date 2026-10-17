namespace Asterion.Configuration;

/// <summary>
///     Locations of everything the bot persists (config, database, logs), all under one directory
/// </summary>
public static class DataPaths
{
    public const string DataDirEnv = "ASTERION_DATA_DIR";

    public static string Root { get; } = Resolve(Environment.GetEnvironmentVariable(DataDirEnv));

    public static string Config => Path.Combine(Root, ConfigKeys.FileName);
    public static string Database => Path.Combine(Root, "data.sqlite");
    public static string Logs => Path.Combine(Root, "logs", "asterion.log");

    /// <summary>
    ///     The directory given by <paramref name="envValue" /> (created if missing), or the application directory
    /// </summary>
    public static string Resolve(string? envValue)
    {
        if (string.IsNullOrWhiteSpace(envValue))
            return AppContext.BaseDirectory;

        var path = Path.GetFullPath(envValue);
        Directory.CreateDirectory(path);
        return path;
    }
}
