using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RinthBot.Services;

public class InteractionCommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _commands;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public InteractionCommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
    {
        _client = client;
        _commands = commands;
        _services = services;

        _logger = services.GetRequiredService<ILogger<InteractionCommandHandler>>();
    }

    public async Task InitializeAsync()
    {
        // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        // Another approach to get the assembly of a specific type is:
        // typeof(CommandHandler).Assembly

        // Process the InteractionCreated payloads to execute Interactions commands
        _client.InteractionCreated += HandleInteraction;

        // Process the command execution results 
        _commands.SlashCommandExecuted += SlashCommandExecuted;
        _commands.ContextCommandExecuted += ContextCommandExecuted;
        _commands.ComponentCommandExecuted += ComponentCommandExecuted;
    }

    # region Error Handling

    private async Task<Task> ComponentCommandExecuted(ComponentCommandInfo arg1, IInteractionContext arg2, IResult arg3)
    {
        if (arg3.IsSuccess) return Task.CompletedTask;
        
        switch (arg3.Error)
        {
            case InteractionCommandError.UnmetPrecondition:
                // Respond with the error reason
                await arg2.Interaction.RespondAsync(arg3.ErrorReason, ephemeral: true);
                break;
            case InteractionCommandError.UnknownCommand:
                // implement
                break;
            case InteractionCommandError.BadArgs:
                // implement
                break;
            case InteractionCommandError.Exception:
                _logger.LogError("Component Command {Arg1Name} for user {UserUsername} failed; Exception: \'{ErrorReason}\'", arg1.Name, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.Unsuccessful:
                // implement
                break;
            case InteractionCommandError.ConvertFailed:
                break;
            case InteractionCommandError.ParseFailed:
                break;
            case null:
                break;
        }

        return Task.CompletedTask;
    }

    private async Task<Task> ContextCommandExecuted(ContextCommandInfo arg1,IInteractionContext arg2, IResult arg3)
    {
        if (arg3.IsSuccess) return Task.CompletedTask;
        
        switch (arg3.Error)
        {
            case InteractionCommandError.UnmetPrecondition:
                await arg2.Interaction.RespondAsync(arg3.ErrorReason, ephemeral: true);
                // implement
                break;
            case InteractionCommandError.UnknownCommand:
                // implement
                break;
            case InteractionCommandError.BadArgs:
                // implement
                break;
            case InteractionCommandError.Exception:
                _logger.LogError("Context Command {Arg1Name} for user {UserUsername} failed; Exception: \'{ErrorReason}\'", arg1.Name, arg2.User.Username, arg3.ErrorReason);
                // implement
                break;
            case InteractionCommandError.Unsuccessful:
                // implement
                break;
            case InteractionCommandError.ConvertFailed:
                break;
            case InteractionCommandError.ParseFailed:
                break;
            case null:
                break;
        }

        return Task.CompletedTask;
    }

    private async Task<Task> SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
    {
        if (arg3.IsSuccess) return Task.CompletedTask;
        
        switch (arg3.Error)
        {
            case InteractionCommandError.UnmetPrecondition:
                await arg2.Interaction.RespondAsync(arg3.ErrorReason, ephemeral: true);
                break;
            case InteractionCommandError.UnknownCommand:
                // implement
                break;
            case InteractionCommandError.BadArgs:
                // implement
                break;
            case InteractionCommandError.Exception:
                _logger.LogError("Slash Command {Arg1Name} for user {UserUsername} failed; Exception: \'{ErrorReason}\'", arg1.Name, arg2.User.Username, arg3.ErrorReason);
                // implement
                break;
            case InteractionCommandError.Unsuccessful:
                // implement
                break;
            case InteractionCommandError.ConvertFailed:
                break;
            case InteractionCommandError.ParseFailed:
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