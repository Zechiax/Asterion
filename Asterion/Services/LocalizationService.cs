using System.Globalization;
using Asterion.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

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
        var localizedString = _localizer[key];
     
        if (localizedString.ResourceNotFound)
        {
            _logger.LogWarning("Missing localization for key: {Key}, report this to the developers!", key);
            return key;
        }
        
        return localizedString;
    }

    public string Get(string key, CultureInfo cultureInfo)
    {
        return _localizer[key, cultureInfo];
    }

    public string Get(string key, object[] parameters)
    {
        return _localizer[key, parameters];
    }

    public string Get(string key, CultureInfo cultureInfo, object[] parameters)
    {
        return _localizer[key, cultureInfo, parameters];
    }
}