using System.Globalization;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.Services;
using RinthBot.Services.Modrinth;
using Serilog;
using RunMode = Discord.Commands.RunMode;

namespace RinthBot
{
    public class Program
    {
        private static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs/rinthbot.log"),
                    rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .CreateLogger();

            new RinthBot().MainAsync().GetAwaiter().GetResult();
        }
    }
}