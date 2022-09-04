using System.Globalization;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.Database;
using RinthBot.Services;
using RinthBot.Services.Modrinth;
using Serilog;
using RunMode = Discord.Commands.RunMode;

namespace RinthBot
{
    public class Program
    {
        private readonly IConfiguration _config;
        private static string? _logLevel;

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            if (args.Count() != 0)
            {
                _logLevel = args[0];
            }
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs/rinthbot.log"), rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .CreateLogger();
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        private async Task MainAsync()
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

            await Task.Delay(Timeout.Infinite);
            
            Log.CloseAndFlush();
        }
        

        private Program()
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

            if (!string.IsNullOrEmpty(_logLevel))
            {
                switch (_logLevel.ToLower())
                {
                    case "info":
                        {
                            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Information);
                            break;
                        }
                    case "error":
                        {
                            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                            break;
                        }
                    case "debug":
                        {
                            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Debug);
                            break;
                        }
                    default:
                        {
                            services.Configure<LoggerFilterOptions>(options => options.MinLevel = LogLevel.Error);
                            break;
                        }
                }
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
}