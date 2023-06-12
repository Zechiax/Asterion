using System.Globalization;
using Figgle;
using Serilog;

namespace Asterion;

public class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "migration") return;

        Console.WriteLine(FiggleFonts.Slant.Render("Asterion v3"));
        
        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs/asterion.log"),
                rollingInterval: RollingInterval.Day)
#if DEBUG
            .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
#endif
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        new Asterion(0).MainAsync().GetAwaiter().GetResult();
    }
}