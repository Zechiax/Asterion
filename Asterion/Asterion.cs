using Asterion.Database;
using Asterion.Interfaces;
using Asterion.Services;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modrinth;
using Quartz;
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

        await services.GetRequiredService<MessageHandler>().InitializeAsync();

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

        // Disconnect from Discord when pressing Ctrl+C
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            logger.LogInformation("{Key} pressed, exiting bot", args.SpecialKey);

            logger.LogInformation("Logging out from Discord");
            client.LogoutAsync().Wait();
            logger.LogInformation("Stopping the client");
            client.StopAsync().Wait();

            logger.LogInformation("Disposing services");
            services.DisposeAsync().GetAwaiter().GetResult();

            args.Cancel = false;
        };

        // We start the stats service after the client has been logged in
        // so that we can get the correct guild count
        services.GetRequiredService<IBotStatsService>().Initialize();

        // We start the scheduler after the client has been logged in
        // so that we can get the correct guild count
        var scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        await scheduler.Start();
        
        await Task.Delay(Timeout.Infinite);
    }

    private ServiceProvider ConfigureServices()
    {
        var config = new DiscordSocketConfig
        {
            AlwaysDownloadUsers = true,
            MessageCacheSize = 100,
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
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

        var services = new ServiceCollection()
            .AddSingleton(_config)
            .AddSingleton(new DiscordSocketClient(config))
            .AddSingleton(new CommandService(commandConfig))
            .AddSingleton<InteractionService>()
            .AddSingleton<InteractionCommandHandler>()
            .AddSingleton<MessageHandler>()
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

        services.AddQuartz(q =>
        {
            q.UseInMemoryStore();
            q.UseMicrosoftDependencyInjectionJobFactory();
        });
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });
        
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