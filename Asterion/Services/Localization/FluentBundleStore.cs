using System.Reflection;
using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Microsoft.Extensions.Logging;

namespace Asterion.Services.Localization;

public class FluentBundleStore
{
    public const string DefaultLocale = "en-US";

    private readonly Dictionary<string, FluentBundle> _bundles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<FluentBundleStore> _logger;

    public FluentBundleStore(ILogger<FluentBundleStore> logger)
    {
        _logger = logger;
        Load();
    }

    public IReadOnlyCollection<string> AvailableLocales => _bundles.Keys;

    public FluentBundle Default => _bundles[DefaultLocale];

    public bool TryGet(string locale, out FluentBundle bundle) => _bundles.TryGetValue(locale, out bundle!);

    private void Load()
    {
        var assembly = Assembly.GetExecutingAssembly();

        const string prefix = "Asterion.Resources.Locales.";

        // MSBuild's embedded-resource logical name generation replaces '-' with '_' in each
        // path segment (it treats the path like a namespace), so "en-US" becomes "en_US" here.
        var byLocale = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".ftl", StringComparison.Ordinal))
            .GroupBy(n => n[prefix.Length..].Split('.')[0].Replace('_', '-'));

        foreach (var group in byLocale)
        {
            var streams = group
                .Select(resourceName => (Reader: (TextReader)new StreamReader(assembly.GetManifestResourceStream(resourceName)!), Name: (string?)resourceName))
                .ToList();

            var ready = LinguiniBuilder.Builder()
                .Locale(group.Key)
                .AddResources(streams);

            var (bundle, errors) = ready.UseConcurrent().Build();

            foreach (var (reader, _) in streams)
                reader.Dispose();

            if (errors is { Count: > 0 })
                foreach (var error in errors)
                    _logger.LogError("Fluent parse error in locale {Locale}: {Error}", group.Key, error);

            _bundles[group.Key] = bundle;
            _logger.LogInformation("Loaded locale {Locale}", group.Key);
        }

        if (!_bundles.ContainsKey(DefaultLocale))
            throw new InvalidOperationException(
                $"No {DefaultLocale} locale found. Check that Resources/Locales/{DefaultLocale}/*.ftl is embedded.");
    }
}
