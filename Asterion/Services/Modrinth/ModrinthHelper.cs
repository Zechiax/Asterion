using System.Text.RegularExpressions;

namespace Asterion.Services.Modrinth;

public class ModrinthHelper
{
    private readonly Regex _modrinthProjectRegex;

    private string ModrinthRegexUrlPattern { get; } =
        @"(?:https:\/\/(?:www.)?modrinth.com\/(mod|modpack|resourcepack|plugin|shader|datapack)\/([a-zA-Z0-9-]*))";

    public ModrinthHelper()
    {
        const RegexOptions regexOptions = RegexOptions.Compiled;
        _modrinthProjectRegex = new Regex(ModrinthRegexUrlPattern, regexOptions);
    }
    
    /// <summary>
    /// Returns true if the url is valid Modrinth Url
    /// </summary>
    /// <param name="url"></param>
    /// <param name="output"></param>
    /// <returns></returns>
    public bool TryParseProjectSlugOrId(string url, out string output)
    {
        output = string.Empty;

        var match = _modrinthProjectRegex.Match(url).Groups.Values.Last();
        
        if (match.Success)
        {
            output = match.Value;
        }

        return match.Success;
    }
}