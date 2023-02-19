using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asterion.Services;

public class InteractionCommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _commands;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    private string _manageSubsGroupError =
        "For this command, you either need to have Administrator permission or have role for managing subs";
    public InteractionCommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
    {
        _client = client;
        _commands = commands;
        _services = services;

        _logger = services.GetRequiredService<ILogger<InteractionCommandHandler>>();
    }

    /// <summary>
    /// Setups Interaction service and registers interaction and error handling functions 
    /// </summary>
    public async Task InitializeAsync()
    {
        // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        // Another approach to get the assembly of a specific type is:
        // typeof(CommandHandler).Assembly

        // Process the InteractionCreated payloads to execute Interactions commands
        _client.InteractionCreated += HandleInteraction;

        // Process the command execution results 
        _commands.SlashCommandExecuted += ApplicationCommandExecuted;
        _commands.ContextCommandExecuted += ApplicationCommandExecuted;
        _commands.ComponentCommandExecuted += ApplicationCommandExecuted;
    }

    # region Error Handling
    private async Task<Task> ApplicationCommandExecuted<T>(T arg1, IInteractionContext arg2, IResult arg3)
    {
        if (arg3.IsSuccess)
            return Task.CompletedTask;
        
        var commandType = "";
        var commandName = "";
        
        switch (arg1)
        {
            case SlashCommandInfo slashCommandInfo:
                commandType = "Slash";
                commandName = slashCommandInfo.Name;
                break;
            case ContextCommandInfo contextCommandInfo:
                commandType = "Context";
                commandName = contextCommandInfo.Name;
                break;
            case ComponentCommandInfo componentCommandInfo:
                commandType = "Component";
                commandName = componentCommandInfo.Name;
                break;
        }

        switch (arg3.Error)
        {
            case InteractionCommandError.UnmetPrecondition:
                if (arg3.ErrorReason.Contains("ManageSubs"))
                {
                    await arg2.Interaction.RespondAsync(_manageSubsGroupError, ephemeral: true);
                    break;
                }
                
                await arg2.Interaction.RespondAsync(arg3.ErrorReason, ephemeral: true);
                break;
            case InteractionCommandError.UnknownCommand:
                _logger.LogError("Unknown {CommandType} Command {Arg1Name} for user {UserUsername}: \'{ErrorReason}\'", commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.BadArgs:
                _logger.LogError("{CommandType} Command {Arg1Name} for user {UserUsername} failed with bad arguments: \'{ErrorReason}\'", commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.Exception:
                _logger.LogError("{CommandType} Command {Arg1Name} for user {UserUsername} failed; Exception: \'{ErrorReason}\'", commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.Unsuccessful:
                _logger.LogError("{CommandType} Command {Arg1Name} for user {UserUsername} failed; Reason: \'{ErrorReason}\'", commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.ConvertFailed:
                _logger.LogError("{CommandType} Command {Arg1Name} for user {UserUsername} failed; Reason: \'{ErrorReason}\'", commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.ParseFailed:
                _logger.LogError("{CommandType} Command {Arg1Name} for user {UserUsername} failed; Reason: \'{ErrorReason}\'", commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case null:
                break;
        }

        return Task.CompletedTask;
    }
    # endregion

    # region Execution

    private async Task HandleInteraction(SocketInteraction arg)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
            var ctx = new SocketInteractionContext(_client, arg);
            await _commands.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            _logger.LogError("{ExceptionMessage}", ex.Message);
            
            // If a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
            // response, or at least let the user know that something went wrong during the command execution.
            if (arg.Type == InteractionType.ApplicationCommand)
                await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
        }
    }
    # endregion
}