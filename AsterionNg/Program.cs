using AsterionNg.Common;
using AsterionNg.Common.Options;
using AsterionNg.Data;
using AsterionNg.Extensions;
using AsterionNg.Search;
using AsterionNg.Search.Providers;
using AsterionNg.Services;
using Discord;
using Discord.Addons.Hosting;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modrinth;
using Serilog;

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/asterion.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddNamedOptions<StartupOptions>();
builder.Services.AddNamedOptions<ReferenceOptions>();

// Add localization services
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddSingleton<IGuildCultureProvider, DefaultGuildCultureProvider>();
builder.Services.AddSingleton<ProjectSearchService>(
    new ProjectSearchService([new ModrinthSearchProvider(new ModrinthClient())]));

builder.Services.AddDbContext<AsterionDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.Configure<ModrinthClientConfig>(config =>
{
    config.UserAgent = "AsterionNg";
});

builder.Services.AddDiscordHost((config, _) =>
{ 
    config.SocketConfig = new DiscordSocketConfig
    {
        LogLevel = LogSeverity.Info,
        GatewayIntents = GatewayIntents.AllUnprivileged,
        LogGatewayIntentWarnings = false,
        UseInteractionSnowflakeDate = false,
        AlwaysDownloadUsers = false,
    };

    var startupOptions = builder.Configuration.GetSection(StartupOptions.GetSectionName()).Get<StartupOptions>();
    
    if (startupOptions is null)
        throw new InvalidOperationException("Startup options are not configured correctly.");
    
    config.Token = startupOptions.Token;
});

builder.Services.AddInteractionService((config, _) =>
{
    config.LogLevel = LogSeverity.Debug;
    config.DefaultRunMode = RunMode.Async;
    config.UseCompiledLambda = true;
});
builder.Services.AddInteractiveService(config =>
{
    config.LogLevel = LogSeverity.Warning;
    config.DefaultTimeout = TimeSpan.FromMinutes(5);
    config.ProcessSinglePagePaginators = true;
});

builder.Services.AddHostedService<InteractionHandler>();

builder.Services.AddSerilog();

var host = builder.Build();

await host.MigrateAsync<AsterionDbContext>();

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}