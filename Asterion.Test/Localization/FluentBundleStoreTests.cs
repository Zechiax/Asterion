using Asterion.Services.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asterion.Test.Localization;

[TestFixture]
public class FluentBundleStoreTests
{
    [Test]
    public void LoadsDefaultLocale()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);

        Assert.That(store.AvailableLocales, Does.Contain(FluentBundleStore.DefaultLocale));
    }

    [Test]
    public void EveryLocaleDefinesTheSameMessageIdsAsTheDefault()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);
        var defaultIds = store.Default.GetMessageEnumerable().ToHashSet();

        foreach (var locale in store.AvailableLocales)
        {
            if (locale == FluentBundleStore.DefaultLocale) continue;

            store.TryGet(locale, out var bundle);
            var ids = bundle.GetMessageEnumerable().ToHashSet();

            Assert.That(ids, Is.EquivalentTo(defaultIds), $"Locale '{locale}' does not define the same message ids as '{FluentBundleStore.DefaultLocale}'");
        }
    }
}
