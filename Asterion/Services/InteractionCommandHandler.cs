using System.Reflection;
using Asterion.Attributes;
using Asterion.Common;
using Asterion.Interfaces;
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
    private readonly ILogger _logger;
    private readonly IServiceProvider _services;

    public InteractionCommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
    {
        _client = client;
        _commands = commands;
        _services = services;

        _logger = services.GetRequiredService<ILogger<InteractionCommandHandler>>();
    }

    /// <summary>
    ///     Setups Interaction service and registers interaction and error handling functions
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
        _commands.ModalCommandExecuted += ApplicationCommandExecuted;
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
            case ModalCommandInfo modalCommandInfo:
                commandType = "Modal";
                commandName = modalCommandInfo.Name;
                break;
        }

        switch (arg3.Error)
        {
            case InteractionCommandError.UnmetPrecondition:
                await SendStatusAsync(arg2, PreconditionCard(arg3));
                break;
            case InteractionCommandError.UnknownCommand:
                _logger.LogError("Unknown {CommandType} Command {Arg1Name} for user {UserUsername}: \'{ErrorReason}\'",
                    commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.BadArgs:
                _logger.LogError(
                    "{CommandType} Command {Arg1Name} for user {UserUsername} failed with bad arguments: \'{ErrorReason}\'",
                    commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                await SendStatusAsync(arg2, Errors.InvalidInput);
                break;
            case InteractionCommandError.Exception:
                // ErrorReason only says that the command failed, the cause is in the exception
                _logger.LogError(arg3 is ExecuteResult result ? result.Exception : null,
                    "{CommandType} Command {Arg1Name} for user {UserUsername} failed; Reason: \'{ErrorReason}\'",
                    commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                await SendStatusAsync(arg2, Errors.CommandFailed);
                break;
            case InteractionCommandError.Unsuccessful:
                _logger.LogError(
                    "{CommandType} Command {Arg1Name} for user {UserUsername} failed; Reason: \'{ErrorReason}\'",
                    commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                break;
            case InteractionCommandError.ConvertFailed:
                _logger.LogError(
                    "{CommandType} Command {Arg1Name} for user {UserUsername} failed; Reason: \'{ErrorReason}\'",
                    commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                await SendStatusAsync(arg2, Errors.InvalidInput);
                break;
            case InteractionCommandError.ParseFailed:
                _logger.LogError(
                    "{CommandType} Command {Arg1Name} for user {UserUsername} failed; Reason: \'{ErrorReason}\'",
                    commandType, commandName, arg2.User.Username, arg3.ErrorReason);
                await SendStatusAsync(arg2, Errors.InvalidInput);
                break;
            case null:
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Card of a failed precondition. Of a failed group (e.g. administrator or manage role), the first one of ours,
    ///     as the built-in preconditions only have English reasons
    /// </summary>
    private static StatusCard PreconditionCard(IResult result)
    {
        return result switch
        {
            LocalizedPreconditionResult localized => localized.Card,
            PreconditionGroupResult group => group.Results.OfType<LocalizedPreconditionResult>().FirstOrDefault()?.Card
                                             ?? Errors.NotAllowed,
            _ => Errors.NotAllowed
        };
    }

    private async Task SendStatusAsync(IInteractionContext context, StatusCard card)
    {
        try
        {
            var localization = _services.GetRequiredService<ILocalizationService>();
            var culture = await _services.GetRequiredService<IGuildCultureProvider>()
                .GetCultureAsync(context.Interaction);

            await InteractionResponder.SendAsync(context.Interaction, card.Build(localization, culture), true);
        }
        catch (Exception e)
        {
            // E.g. the interaction expired, nothing more can be sent
            _logger.LogWarning(e, "Couldn't send error {Key} to user {UserUsername}", card.Key,
                context.User.Username);
        }
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
            _logger.LogError(ex, "Executing interaction {InteractionId} failed", arg.Id);

            // If a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
            // response, or at least let the user know that something went wrong during the command execution.
            if (arg.Type == InteractionType.ApplicationCommand)
                await arg.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
    }

    # endregion
}