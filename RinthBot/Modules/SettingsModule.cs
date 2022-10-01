using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RinthBot.ComponentBuilders;
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
        var embed = SettingsEmbedBuilder.GetIntroEmbedBuilder();
        var components = SettingsComponentBuilder.GetIntroButtons();

        await RespondAsync(embed: embed.Build(), components: components.Build());
    }
}