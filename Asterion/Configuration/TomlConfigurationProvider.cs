using System.Globalization;
using Microsoft.Extensions.Configuration;
using Tomlyn;
using Tomlyn.Model;

namespace Asterion.Configuration;

public class TomlConfigurationSource : IConfigurationSource
{
    public required string Path { get; init; }

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new TomlConfigurationProvider(Path);
}

public class TomlConfigurationProvider(string path) : ConfigurationProvider
{
    public override void Load()
    {
        var table = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))
                    ?? throw new InvalidDataException($"Could not parse {path}");

        Data = table.ToDictionary(kv => kv.Key,
            kv => Convert.ToString(kv.Value, CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
    }
}

public static class TomlConfigurationExtensions
{
    public static IConfigurationBuilder AddTomlFile(this IConfigurationBuilder builder, string path) =>
        builder.Add(new TomlConfigurationSource { Path = path });
}
