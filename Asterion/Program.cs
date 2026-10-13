using System.Globalization;
using Asterion.Configuration;
using Figgle.Fonts;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Extensions.Logging;

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

        var configPath = Path.Combine(AppContext.BaseDirectory, ConfigKeys.FileName);
        IConfiguration config;
        try
        {
            ConfigBootstrapper.EnsureConfigFile(AppContext.BaseDirectory, new SerilogLoggerFactory(Log.Logger).CreateLogger("Config"));
            config = new ConfigurationBuilder().AddTomlFile(configPath).Build();
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Could not load configuration from {Path}", configPath);
            Log.CloseAndFlush();
            Environment.Exit(1);
            return;
        }

        var errors = ConfigValidator.Validate(config, Asterion.IsDebug());
        if (errors.Count > 0)
        {
            Log.Fatal("Invalid configuration in {Path}, the bot will not start:", configPath);
            foreach (var error in errors)
                Log.Fatal(" - {Error}", error);
            Log.CloseAndFlush();
            Environment.Exit(1);
            return;
        }

        new Asterion(0, config).MainAsync().GetAwaiter().GetResult();
    }
}