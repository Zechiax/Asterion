using System.Globalization;
using Figgle.Fonts;
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
#endif
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Information) // Set the minimum log level for EF Core messages
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning) // Set the minimum log level for EF Core command messages
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        new Asterion(0).MainAsync().GetAwaiter().GetResult();
    }
}