using System.Globalization;
using Serilog;

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
#if DEBUG
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Debug)
#else
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
#endif
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            new RinthBot().MainAsync().GetAwaiter().GetResult();
        }
    }
}