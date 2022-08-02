using Discord;
using Microsoft.Extensions.Logging;

namespace RinthBot.Extensions;

public static class LogExtensions
{
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