using System.Globalization;

namespace Asterion.Interfaces;

public interface ILocalizationService
{
    /// <summary>Looks up <paramref name="key"/> in <paramref name="culture"/>, falling back to en-US.</summary>
    string Get(string key, CultureInfo? culture = null);

    string Get(string key, CultureInfo? culture, object args);
}
