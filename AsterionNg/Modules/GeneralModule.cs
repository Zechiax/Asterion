using System.Reflection;
using AsterionNg.Assets;
using AsterionNg.Common;
using AsterionNg.Common.Options;
using AsterionNg.Extensions.Builders;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace AsterionNg.Modules;

public class GeneralModule(
    IOptions<ReferenceOptions> options,
    IStringLocalizerFactory factory,
    IGuildCultureProvider? cultureProvider) : ModuleBase(factory, cultureProvider)
{
    [SlashCommand("about", "Shows information about the app.")]
    public async Task AboutAsync()
    {
        var app = await Context.Client.GetApplicationInfoAsync();

        var embed = new EmbedBuilder()
            .WithTitle(app.Name)
            .WithDescription(app.Description)
            .AddField(L["About.Guilds"], Context.Client.Guilds.Count, true)
            .AddField(L["About.Latency"], Context.Client.Latency + "ms", true)
            .AddField(L["About.Version"], Assembly.GetExecutingAssembly().GetName().Version, true)
            .WithAuthor(app.Owner.Username, app.Owner.GetDisplayAvatarUrl())
            .WithFooter(string.Join(" · ", app.Tags.Select(t => '#' + t)))
            .WithColor(Colors.Primary)
            .Build();

        var components = new ComponentBuilder()
            .WithLink(L["About.Support"], Emotes.Logos.Discord, options.Value.SupportServerUrl)
            .WithLink(L["About.Source"], Emotes.Logos.Github, options.Value.SourceRepositoryUrl)
            .Build();

        await RespondAsync(embed: embed, components: components);
    }
}