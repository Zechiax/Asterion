using Asterion.Database;
using Asterion.Interfaces;
using Asterion.Services;
using Asterion.Services.Modrinth;
using Asterion.Services.Notifications;
using Asterion.Services.Notifications.Delivery;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modrinth;
using Serilog;
using RunMode = Discord.Commands.RunMode;

namespace Asterion;

public class Asterion
{
    private readonly IConfiguration _config;
    private int _shardId;

    public Asterion(int shardId)
    {
        _shardId = shardId;
        
        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("config.json", false, true)
            .Build();
    }

    public async Task MainAsync()
    {
        await using var services = ConfigureServices();

        // Setup logging
        services.GetRequiredService<LoggingService>();

        // Run database migration
        services.GetRequiredService<DatabaseMigrationService>().MigrateDatabase();

        var client = services.GetRequiredService<DiscordSocketClient>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        var commands = services.GetRequiredService<InteractionService>();

        services.GetRequiredService<ModrinthService>();

        // Setup interaction command handler
        await services.GetRequiredService<InteractionCommandHandler>().InitializeAsync();

        // Initialize data service after client has been connected
        client.Ready += services.GetRequiredService<IDataService>().InitializeAsync;
        services.GetRequiredService<ClientService>().Initialize();

        var commandsRegistered = false;
        client.Ready += async () =>
        {
            if (commandsRegistered)
                return;

            if (IsDebug())
            {
                var testGuildId = _config.GetValue<ulong>("testGuild");
                logger.LogInformation("Registering commands to test guild ID {Value}", testGuildId);

                await commands.RegisterCommandsToGuildAsync(testGuildId);
            }
            else
            {
                logger.LogInformation("Registering commands globally");
                await commands.RegisterCommandsGloballyAsync();
            }

            commandsRegistered = true;
        };

        await client.LoginAsync(TokenType.Bot, _config.GetValue<string>("token"));
        await client.StartAsync();

        // We start the stats service after the client has been logged in
        // so that we can get the correct guild count
        services.GetRequiredService<IBotStatsService>().Initialize();

        // This app builds a plain ServiceProvider rather than a Generic Host, so nothing starts
        // registered IHostedServices automatically - we have to do it ourselves, after the client
        // has logged in so background services relying on guild/client state see it as ready.
        var hostedServices = services.GetServices<IHostedService>().ToList();
        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(CancellationToken.None);

        // Disconnect from Discord when pressing Ctrl+C
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;

            logger.LogInformation("{Key} pressed, exiting bot", args.SpecialKey);

            logger.LogInformation("Stopping background services");
            foreach (var hostedService in hostedServices)
                hostedService.StopAsync(CancellationToken.None).Wait();

            logger.LogInformation("Logging out from Discord");
            client.LogoutAsync().Wait();
            logger.LogInformation("Stopping the client");
            client.StopAsync().Wait();

            args.Cancel = false;
        };

        await Task.Delay(Timeout.Infinite);
    }

    private ServiceProvider ConfigureServices()
    {
        var config = new DiscordSocketConfig
        {
            AlwaysDownloadUsers = true,
            MessageCacheSize = 100,
            GatewayIntents = GatewayIntents.AllUnprivileged
        };

        var commandConfig = new CommandServiceConfig
        {
            DefaultRunMode = RunMode.Async
        };

        var modrinthClientConfig = new ModrinthClientConfig
        {
            UserAgent = "Zechiax/Asterion",
            RateLimitRetryCount = 3
        };
        
        var client = new DiscordSocketClient(config);

        var services = new ServiceCollection()
            .AddSingleton(_config)
            .AddSingleton(client)
            .AddSingleton(new CommandService(commandConfig))
            .AddSingleton<InteractionService>( new InteractionService(client))
            .AddSingleton<InteractionCommandHandler>()
            .AddSingleton<LoggingService>()
            .AddSingleton<IDataService, DataService>()
            .AddSingleton<ModrinthService>()
            .AddSingleton<InteractiveService>()
            .AddSingleton<ClientService>()
            .AddSingleton<IModrinthClient>(new ModrinthClient(modrinthClientConfig))
            .AddSingleton<DatabaseMigrationService>()
            .AddSingleton<ProjectStatisticsManager>()
            .AddHttpClient()
            .AddDbContext<DataContext>()
            .AddSingleton<IBotStatsService, BotStatsService>()
            .AddSingleton<ILocalizationService, LocalizationService>()
            .AddMemoryCache()
            .AddLogging(configure => configure.AddSerilog(dispose: true));

        // Notification outbox/dispatch pipeline
        services
            .AddSingleton<INotificationScheduler, NotificationScheduler>()
            .AddSingleton<INotificationReader, NotificationReader>()
            .AddSingleton<INotificationSignal, NotificationSignal>()
            .AddSingleton<INotificationSender, BotSender>()
            .AddSingleton<INotificationSender, WebhookSender>()
            .AddSingleton<WebhookRateLimiter>()
            .AddSingleton<NotificationSenderRouter>()
            // Registered as itself too (not just IHostedService) so it can also be resolved and invoked
            // directly, sharing the same singleton instance the host runs.
            .AddSingleton<UpdateDetectionService>()
            .AddHostedService(sp => sp.GetRequiredService<UpdateDetectionService>())
            .AddHostedService<NotificationDispatchService>()
            .AddHostedService<MaintenanceService>()
            .AddHostedService<BotActivityRefreshService>();

        services.AddLocalization(options =>
        {
            options.ResourcesPath = "Resources";
        });

        if (IsDebug())
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
        else
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider;
    }

    private static bool IsDebug()
    {
#if DEBUG
        return true;
#else
            return false;
#endif
    }
}