using Discord.Interactions;
using Linguini.Bundle;

namespace Asterion.Services.Localization;

public class FluentLocalizationManager(FluentBundleStore store) : ILocalizationManager
{
    public IDictionary<string, string> GetAllNames(IList<string> key, LocalizationTarget destinationType)
        => GetAll(key, "name");

    public IDictionary<string, string> GetAllDescriptions(IList<string> key, LocalizationTarget destinationType)
        => GetAll(key, "description");

    private IDictionary<string, string> GetAll(IList<string> key, string attribute)
    {
        var messageId = "command-" + string.Join("-", key);
        var result = new Dictionary<string, string>();

        foreach (var locale in store.AvailableLocales)
        {
            if (locale == FluentBundleStore.DefaultLocale) continue; // the attribute literal is already the default
            if (!store.TryGet(locale, out var bundle)) continue;
            if (bundle.TryGetMessage(messageId, attribute, null, out _, out var value))
                result[locale] = value!;
        }

        return result;
    }
}
