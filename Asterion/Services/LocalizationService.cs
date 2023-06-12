using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Asterion.Interfaces;

public class LocalizationService : ILocalizationService
{
    private readonly IStringLocalizer _localizer;
    private readonly ILogger<LocalizationService> _logger;

    public LocalizationService(IStringLocalizerFactory localizer, ILogger<LocalizationService> logger)
    {
        _localizer = localizer.Create("responses", "Asterion");
        _logger = logger;
    }
    
    public string Get(string key)
    {
        _logger.LogDebug("Getting localized string for key {Key}", key);
        var localizedString = _localizer[key];
        _logger.LogDebug("Localized string for key {Key} is {LocalizedString}", key, localizedString);
        return localizedString;
    }

    public string Get(string key, CultureInfo cultureInfo)
    {
        return _localizer[key, cultureInfo];
    }
}