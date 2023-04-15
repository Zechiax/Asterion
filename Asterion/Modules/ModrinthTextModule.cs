using Asterion.ComponentBuilders;
using Asterion.EmbedBuilders;
using Asterion.Extensions;
using Asterion.Interfaces;
using Asterion.Services.Modrinth;
using Discord;
using Discord.Commands;

namespace Asterion.Modules;

public class ModrinthTextModule : ModuleBase<SocketCommandContext>
{
    private readonly IDataService _data;
    private readonly ModrinthService _modrinth;

    public ModrinthTextModule(ModrinthService modrinth, IDataService data)
    {
        _modrinth = modrinth;
        _data = data;
    }

    [RequireBotPermission(ChannelPermission.SendMessages)]
    [Command("project", RunMode = RunMode.Async)]
    public async Task FindProject(string slugOrId)
    {
        // If we get any exception, abort
        var searchResult = await _modrinth.FindProject(slugOrId);
        var team = await _modrinth.GetProjectsTeamMembersAsync(slugOrId);

        if (searchResult.Success == false) return;

        var embed = ModrinthEmbedBuilder.GetProjectEmbed(searchResult, team);
        var components = new ComponentBuilder();

        var guild = await _data.GetGuildByIdAsync(Context.Guild.Id);

        if (guild is null)
            // Won't do anything
            return;

        if ((bool) guild.GuildSettings.ShowSubscribeButton!)
        {
            var guildSubscribed =
                await _data.IsGuildSubscribedToProjectAsync(Context.Guild.Id, searchResult.Payload.Project.Id);
            components.WithButton(ModrinthComponentBuilder.GetSubscribeButtons(Context.User.Id,
                searchResult.Payload.Project.Id,
                !guildSubscribed));
        }

        components.WithButton(ModrinthComponentBuilder.GetProjectLinkButton(searchResult.Payload.Project))
            .WithButton(ModrinthComponentBuilder.GetUserToViewButton(Context.User.Id, team.GetOwner()?.User.Id,
                searchResult.Payload.Project.Id));

        try
        {
            await Context.Message.ReplyAsync(embed: embed.Build(), components: components.Build(),
                allowedMentions: AllowedMentions.None);
        }
        catch (Exception)
        {
            // ignored
        }
    }
}