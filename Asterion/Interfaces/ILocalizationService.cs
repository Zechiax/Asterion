using System.Globalization;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace Asterion.Interfaces;

public interface ILocalizationService
{
    LocalizedString Get(string key);
    LocalizedString Get(string key, CultureInfo? cultureInfo);
    LocalizedString Get(string key, params object[] parameters);
    LocalizedString Get(string key, CultureInfo? cultureInfo, params object[] parameters);
}