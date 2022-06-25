using System.Globalization;

namespace RinthBot.Extensions;

public static class IntExtensions
{
    public static string SeparateThousands(this int n, string separator = " ")
    {
        // https://stackoverflow.com/a/17527989
        var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        nfi.NumberGroupSeparator = separator;
        
        return n.ToString("#,0", nfi);
    }
}