using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.EmbedBuilders;
using RinthBot.Interfaces;

namespace RinthBot.Modules;

public class SettingsModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<SettingsModule> _logger;
    private readonly IDataService _dataService;

    public SettingsModule(IServiceProvider serviceProvider)
    {
        _logger = serviceProvider.GetRequiredService<ILogger<SettingsModule>>();
        _dataService = serviceProvider.GetRequiredService<IDataService>();
    }

    [SlashCommand("settings", "Change settings for RinthBot")]
    public async Task SettingsCommand()
    {
        await DeferAsync();

        var guild = await _dataService.GetGuildByIdAsync(Context.Guild.Id);

        if (guild is null)
        {
            await FollowupAsync("Sorry, there was an internal error, please try again later");
            return;
        }

        var embed = SettingsEmbedBuilder.GetIntroEmbedBuilder();
        
    }
}