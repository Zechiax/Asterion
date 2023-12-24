using System.Globalization;
using Asterion.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

public class LocalizationService : ILocalizationService
{
    private readonly IStringLocalizer _localizer;
    private readonly ILogger<LocalizationService> _logger;
    private readonly CultureInfo _defaultCultureInfo = CultureInfo.GetCultureInfo("en-US");

    public LocalizationService(IStringLocalizerFactory localizer, ILogger<LocalizationService> logger)
    {
        _localizer = localizer.Create("responses", "Asterion");
        _logger = logger;
    }
    
    public LocalizedString Get(string key)
    {
        var localizedString = _localizer[key];
     
        if (localizedString.ResourceNotFound)
        {
            _logger.LogWarning("Missing localization for key: {Key}, report this to the developers!", key);
            return localizedString;
        }
        
        return localizedString;
    }

    public LocalizedString Get(string key, CultureInfo? cultureInfo)
    {
        // We have to set the culture of the current thread to the culture we want to use
        SetCulture(cultureInfo);

        return _localizer[key];
    }

    public LocalizedString Get(string key, params object[] parameters)
    {
        return _localizer[key, parameters];
    }

    public LocalizedString Get(string key, CultureInfo? cultureInfo, params object[] parameters)
    {
        SetCulture(cultureInfo);
        
        return _localizer[key, parameters];
    }
    
    private void SetCulture(CultureInfo? cultureInfo)
    {
        CultureInfo.CurrentCulture = cultureInfo ?? _defaultCultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo ?? _defaultCultureInfo;
    }
}