using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.Services;
using RinthBot.Services.Modrinth;
using Serilog;
using RunMode = Discord.Commands.RunMode;

namespace RinthBot;

public class RinthBot
{
    private readonly IConfiguration _config;

    public RinthBot()
    {
        //Create the configuration and build
        if (!File.Exists("config.toml"))
        {
            throw new FileNotFoundException("There is no configuration file present in program directory");
        }

        _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddTomlFile(path: "config.toml", false, true)
            .Build();
    }

    public async Task MainAsync()
    {
        await using var services = ConfigureServices();

        // Setup logging
        services.GetRequiredService<LoggingService>();

        var client = services.GetRequiredService<DiscordSocketClient>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        var commands = services.GetRequiredService<InteractionService>();

        services.GetRequiredService<ModrinthService>();

        // Setup interaction command handler
        await services.GetRequiredService<InteractionCommandHandler>().InitializeAsync();

        // Initialize data service after client has been connected
        client.Ready += services.GetRequiredService<DataService>().InitializeAsync;
        services.GetRequiredService<ClientService>().Initialize();

        client.Ready += async () =>
        {
            if (IsDebug())
            {
                var testGuildId = _config.GetSection("guilds").GetSection("test").GetValue<ulong>("id");
                logger.LogInformation("Registering commands to test guild (D) ID {Value}", testGuildId);

                await commands.RegisterCommandsToGuildAsync(testGuildId);
            }
            else
            {
                logger.LogInformation("Registering commands globally");
                await commands.RegisterCommandsGloballyAsync();
            }
        };

        var clientSection = _config.GetSection("client");
        await client.LoginAsync(TokenType.Bot, clientSection["token"]);
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

            Log.CloseAndFlush();

            args.Cancel = false;
        };

        await Task.Delay(Timeout.Infinite);
    }

    private ServiceProvider ConfigureServices()
    {
        var config = new DiscordSocketConfig
        {
            //AlwaysDownloadUsers = true,
            MessageCacheSize = 100
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
            .AddSingleton<LoggingService>()
            .AddSingleton<DataService>()
            .AddSingleton<ModrinthService>()
            .AddSingleton<InteractiveService>()
            .AddSingleton<ClientService>()
            .AddMemoryCache()
            .AddLogging(configure => configure.AddSerilog());

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