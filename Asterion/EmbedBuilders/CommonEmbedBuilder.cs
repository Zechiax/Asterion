using Discord;
using Color = Discord.Color;

namespace Asterion.EmbedBuilders;

public static class CommonEmbedBuilder
{
    private static Color _errorColor = Color.Red;
    private static Color _warningColor = Color.Orange;
    private static Color _infoColor = Color.Blue;
    private static Color _successColor = Color.Green;
    
    public static EmbedBuilder GetSuccessEmbedBuilder(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(_successColor)
            .WithCurrentTimestamp();
    }
    
    public static EmbedBuilder GetErrorEmbedBuilder(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(_errorColor)
            .WithCurrentTimestamp();
    }
    
    public static EmbedBuilder GetInfoEmbedBuilder(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(_infoColor)
            .WithCurrentTimestamp();
    }
    
    public static EmbedBuilder GetWarningEmbedBuilder(string title, string description)
    {
        return new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(_warningColor)
            .WithCurrentTimestamp();
    }
}