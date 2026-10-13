using System.Collections.Concurrent;
using System.Globalization;
using Asterion.Interfaces;
using Linguini.Bundle;
using Linguini.Shared.Types.Bundle;
using Microsoft.Extensions.Logging;

namespace Asterion.Services.Localization;

public class LocalizationService : ILocalizationService
{
    private readonly FluentBundleStore _store;
    private readonly ILogger<LocalizationService> _logger;

    // So a missing key logs once, not once per interaction.
    private readonly ConcurrentDictionary<string, byte> _warned = new();

    public LocalizationService(FluentBundleStore store, ILogger<LocalizationService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public string Get(string key, CultureInfo? culture = null) => Get(key, culture, null!);

    public string Get(string key, CultureInfo? culture, object args)
    {
        var fluentArgs = ToFluentArgs(args);

        foreach (var candidate in Candidates(culture))
            if (_store.TryGet(candidate, out var bundle)
                && bundle.TryGetMessage(key, fluentArgs, out _, out var message))
                return message!;

        if (_warned.TryAdd(key, 0))
            _logger.LogWarning("Missing localization for key {Key}, report this to the developers!", key);

        return key;
    }

    private static IEnumerable<string> Candidates(CultureInfo? culture)
    {
        if (culture is not null)
        {
            yield return culture.Name;
            if (culture.Parent is { Name.Length: > 0 } p)
                yield return p.Name;
        }

        yield return FluentBundleStore.DefaultLocale;
    }

    private static IDictionary<string, IFluentType>? ToFluentArgs(object? args)
    {
        if (args is null) return null;

        var dict = new Dictionary<string, IFluentType>();
        foreach (var prop in args.GetType().GetProperties())
        {
            var value = prop.GetValue(args);
            dict[prop.Name] = value switch
            {
                null => FluentNone.None,
                int or long or double or float or decimal => FluentNumber.FromString(value.ToString()!),
                _ => (FluentString)value.ToString()!
            };
        }

        return dict;
    }
}
