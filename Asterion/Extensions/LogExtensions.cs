using Discord;
using Microsoft.Extensions.Logging;

namespace Asterion.Extensions;

public static class LogExtensions
{
    /// <summary>
    /// Converts DiscordNet's LogSeverity to Serilog's LogLevel 
    /// </summary>
    /// <param name="logSeverity"></param>
    /// <returns></returns>
    public static LogLevel ToLogLevel(this LogSeverity logSeverity)
    {
        return logSeverity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            LogSeverity.Error => LogLevel.Error,
            _ => LogLevel.None
        };
    }
}