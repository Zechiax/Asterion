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
using Serilog;
using RunMode = Discord.Commands.RunMode;

namespace Asterion;

public class RinthBot
{
    private readonly IConfiguration _config;

    public RinthBot()
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(path: "config.json", false, true)
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

        client.Ready += async () =>
        {
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

        var commandConfig = new CommandServiceConfig()
        {
            DefaultRunMode = RunMode.Async
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
            .AddSingleton<DatabaseMigrationService>()
            .AddHttpClient()
            .AddDbContext<DataContext>()
            .AddMemoryCache()
            .AddLogging(configure => configure.AddSerilog(dispose: true));

        if (IsDebug())
        {
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
        }
        else
        {
            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
        }

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