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
                .WriteTo.Console()
                .CreateLogger();

            new RinthBot().MainAsync().GetAwaiter().GetResult();
        }
    }
}