using System.Globalization;
using Asterion.Common;
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

        Console.WriteLine(FiggleFonts.Slant.Render("Asterion"));
        Console.WriteLine($"v{AppVersion.Display}");

        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(DataPaths.Logs, rollingInterval: RollingInterval.Day)
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

        var configPath = DataPaths.Config;
        IConfiguration config;
        try
        {
            ConfigBootstrapper.EnsureConfigFile(DataPaths.Root, new SerilogLoggerFactory(Log.Logger).CreateLogger("Config"));
            config = new ConfigurationBuilder()
                .AddTomlFile(configPath)
                .AddEnvironmentVariables(ConfigKeys.EnvPrefix)
                .Build();
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

        try
        {
            new Asterion(0, config).MainAsync().GetAwaiter().GetResult();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}