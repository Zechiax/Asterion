using System.Globalization;

namespace AsterionNg.Common;

public interface IGuildCultureProvider
{
    CultureInfo GetGuildCulture(ulong guildId);
}

public class DefaultGuildCultureProvider : IGuildCultureProvider
{
    public CultureInfo GetGuildCulture(ulong guildId)
    {
        // TODO: Get from database
        return CultureInfo.GetCultureInfo("en-US");
    }
}