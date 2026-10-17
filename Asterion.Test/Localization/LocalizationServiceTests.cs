using System.Globalization;
using Asterion.Services.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asterion.Test.Localization;

[TestFixture]
public class LocalizationServiceTests
{
    private LocalizationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);
        _service = new LocalizationService(store, NullLogger<LocalizationService>.Instance);
    }

    [Test]
    public void ReturnsKeyItselfForUnknownKey()
    {
        Assert.That(_service.Get("this-key-does-not-exist"), Is.EqualTo("this-key-does-not-exist"));
    }

    [Test]
    public void SubstitutesNamedArgument()
    {
        var result = _service.Get("modrinth-search-no-result", null, new { query = "sodium" });

        Assert.That(result, Is.EqualTo("No results for query 'sodium'"));
    }

    [Test]
    public void FallsBackFromRegionalToLanguageToDefault()
    {
        var result = _service.Get("error-unknown", CultureInfo.GetCultureInfo("cs-CZ"));

        Assert.That(result, Is.EqualTo("Unknown error"));
    }

    [Test]
    public void LogsMissingKeyOnceNotOncePerLookup()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);
        var logger = new CountingLogger<LocalizationService>();
        var service = new LocalizationService(store, logger);

        service.Get("this-key-does-not-exist");
        service.Get("this-key-does-not-exist");

        Assert.That(logger.WarningCount, Is.EqualTo(1));
    }

    private class CountingLogger<T> : ILogger<T>
    {
        public int WarningCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) WarningCount++;
        }
    }
}
