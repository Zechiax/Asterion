using System.Runtime.InteropServices;
using Asterion.Common;
using Asterion.Configuration;
using Asterion.Database;
using Asterion.Interfaces;
using Asterion.Services;
using Asterion.Services.Follows;
using Asterion.Services.Localization;
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
    // Leaves room for the Discord logout within Docker's default 10 second stop grace period
    private static readonly TimeSpan BackgroundServicesStopTimeout = TimeSpan.FromSeconds(5);

    private readonly IConfiguration _config;
    private int _shardId;

    public Asterion(int shardId, IConfiguration config)
    {
        _shardId = shardId;
        _config = config;
    }

    public async Task MainAsync()
    {
        await using var services = ConfigureServices();

        // Setup logging
        services.GetRequiredService<LoggingService>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        using var shutdown = new CancellationTokenSource();
        void OnShutdownSignal(PosixSignalContext context)
        {
            if (shutdown.IsCancellationRequested)
                return;

            context.Cancel = true;
            logger.LogInformation("Received {Signal}, exiting bot", context.Signal);
            shutdown.Cancel();
        }

        using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnShutdownSignal);
        using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnShutdownSignal);
        using var sigQuit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, OnShutdownSignal);

        // Run database migration
        services.GetRequiredService<DatabaseMigrationService>().MigrateDatabase();

        var client = services.GetRequiredService<DiscordSocketClient>();
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
                var testGuildId = _config.GetValue<ulong>(ConfigKeys.TestGuild);
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

        await client.LoginAsync(TokenType.Bot, _config.GetValue<string>(ConfigKeys.Token));
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

        await Task.Delay(Timeout.Infinite, shutdown.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        logger.LogInformation("Stopping background services");
        using (var stopTimeout = new CancellationTokenSource(BackgroundServicesStopTimeout))
        {
            try
            {
                await Task.WhenAll(hostedServices.Select(s => s.StopAsync(stopTimeout.Token)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop background services");
            }
        }

        logger.LogInformation("Logging out from Discord");
        await client.LogoutAsync();
        logger.LogInformation("Stopping the client");
        await client.StopAsync();
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
            UserAgent = $"Zechiax/Asterion/{AppVersion.Core} (https://github.com/Zechiax/Asterion)",
            RateLimitRetryCount = 3
        };
        
        var client = new DiscordSocketClient(config);

        var services = new ServiceCollection()
            .AddSingleton(_config)
            .AddSingleton(client)
            .AddSingleton(new CommandService(commandConfig))
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
            .AddSingleton<FluentBundleStore>()
            .AddSingleton<ILocalizationService, LocalizationService>()
            .AddSingleton<IGuildCultureProvider, GuildCultureProvider>()
            .AddMemoryCache()
            .AddLogging(configure => configure.AddSerilog(dispose: true));

        services.AddSingleton<InteractionService>(sp => new InteractionService(client, new InteractionServiceConfig
        {
            LocalizationManager = new FluentLocalizationManager(sp.GetRequiredService<FluentBundleStore>())
        }));

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
            .AddHostedService<FollowSyncService>()
            .AddHostedService<BotActivityRefreshService>();

        // todo: later organizations
        services
            .AddSingleton<IFollowSource, UserFollowSource>()
            .AddSingleton<IFollowService, FollowService>();

        if (IsDebug())
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
        else
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider;
    }

    public static bool IsDebug()
    {
#if DEBUG
        return true;
#else
            return false;
#endif
    }
}