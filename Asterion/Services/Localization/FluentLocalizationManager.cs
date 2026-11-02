using Discord.Interactions;
using Linguini.Bundle;

namespace Asterion.Services.Localization;

public class FluentLocalizationManager(FluentBundleStore store) : ILocalizationManager
{
    // Command and option names stay in English everywhere: the texts of the bot refer to them (e.g. `/manage`)
    // and the same name in docs and support is easier to find than one per language
    public IDictionary<string, string> GetAllNames(IList<string> key, LocalizationTarget destinationType)
        => new Dictionary<string, string>();

    public IDictionary<string, string> GetAllDescriptions(IList<string> key, LocalizationTarget destinationType)
    {
        var messageId = "command-" + string.Join("-", key);
        var result = new Dictionary<string, string>();

        foreach (var locale in store.AvailableLocales)
        {
            if (locale == FluentBundleStore.DefaultLocale) continue; // the attribute literal is already the default
            if (!store.TryGet(locale, out var bundle)) continue;
            if (bundle.TryGetMessage(messageId, "description", null, out _, out var value))
                result[locale] = value!;
        }

        return result;
    }
}
