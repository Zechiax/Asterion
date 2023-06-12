using System.Globalization;
using Discord.Interactions;

namespace Asterion.Interfaces;

public interface ILocalizationService
{
    string Get(string key);
    string Get(string key, CultureInfo cultureInfo);
    string Get(string key, object[] parameters);
    string Get(string key, CultureInfo cultureInfo, object[] parameters);
}