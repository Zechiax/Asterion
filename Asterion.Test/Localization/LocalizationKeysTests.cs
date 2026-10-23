using System.Text.RegularExpressions;
using Asterion.Services.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asterion.Test.Localization;

[TestFixture]
public partial class LocalizationKeysTests
{
    // Keys passed to the localization service or a Loc helper as a literal, and every error or warning key,
    // e.g. in Errors.cs. Keys built at runtime (interpolated) aren't checked
    private static readonly Regex KeyLiteral =
        LocalizationRegex();

    [Test]
    public void EveryKeyUsedInCodeExistsInTheDefaultLocale()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);
        var ids = store.Default.GetMessageEnumerable().ToHashSet();

        var missing = Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "Asterion"), "*.cs",
                SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(file => KeyLiteral.Matches(File.ReadAllText(file))
                .Select(match => (Key: match.Groups["key"].Value, File: Path.GetFileName(file))))
            .Where(usage => !ids.Contains(usage.Key))
            .Select(usage => $"{usage.Key} ({usage.File})")
            .Distinct()
            .ToList();

        Assert.That(missing, Is.Empty, "Keys used in code but missing in the .ftl files");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Asterion.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Asterion.sln not found");
    }

    [GeneratedRegex(@"\b(?:\w*[Ll]ocalizationService\.Get|loc\.Get|Loc)\(\s*""(?<key>[a-z0-9-]+)""|""(?<key>(?:error|warning)-[a-z0-9-]+)""")]
    private static partial Regex LocalizationRegex();
}
