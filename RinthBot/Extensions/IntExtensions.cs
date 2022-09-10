using System.Globalization;
using Humanizer;

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

    public static string ToModrinthFormat(this int n)
    {
        return n.ToMetric(decimals: 1).Transform(To.UpperCase);
    }
}