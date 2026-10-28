using System.Reflection;

namespace Asterion.Common;

public static class AppVersion
{
    private const int ShortCommitLength = 7;

    private static readonly string Informational =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

    /// <summary>Version without the build metadata, f.e. <c>4.0.0</c></summary>
    public static string Core { get; } = StripMetadata(Informational);

    /// <summary>Version with a shortened commit hash, f.e. <c>4.0.0+1a2b3c4d</c></summary>
    public static string Display { get; } = Shorten(Informational);

    private static string StripMetadata(string version)
    {
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    private static string Shorten(string version)
    {
        var plus = version.IndexOf('+');
        return plus >= 0 && version.Length > plus + 1 + ShortCommitLength ? version[..(plus + 1 + ShortCommitLength)] : version;
    }
}
