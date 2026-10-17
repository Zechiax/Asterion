using System.Runtime.CompilerServices;
using Asterion.Interfaces;
using Asterion.Modules;
using Asterion.Services.Modrinth;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Modrinth;
using NSubstitute;

namespace Asterion.Test.Follows;

[TestFixture]
public class FollowModuleTests
{
    [Test]
    public async Task ManageModulesBuildWithExpectedCommands()
    {
        using var client = new DiscordSocketClient();
        using var interactions = new InteractionService(client);
        await using var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ILocalizationService>())
            .AddSingleton(Substitute.For<IGuildCultureProvider>())
            .AddSingleton(Substitute.For<IFollowService>())
            .AddSingleton(Substitute.For<IDataService>())
            .AddSingleton(Substitute.For<IModrinthClient>())
            // Only constructed, never called
            .AddSingleton((ModrinthService)RuntimeHelpers.GetUninitializedObject(typeof(ModrinthService)))
            .BuildServiceProvider();

        var follow = await interactions.AddModuleAsync<FollowModule>(services);
        await interactions.AddModuleAsync<FollowInteractionModule>(services);
        await interactions.AddModuleAsync<EntryInteractionModule>(services);
        var manage = await interactions.AddModuleAsync<ManageInteractionModule>(services);
        await interactions.AddModuleAsync<ServerSettingsInteractionModule>(services);
        await interactions.AddModuleAsync<LegacySettingsInteractionModule>(services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(follow.SlashGroupName, Is.EqualTo("follow"));
            Assert.That(follow.SlashCommands.Select(c => c.Name), Is.EquivalentTo(new[] { "user" }));
            Assert.That(manage.SlashCommands.Select(c => c.Name), Is.EquivalentTo(new[] { "manage" }));
            Assert.That(interactions.ModalCommands.Select(c => c.Name), Is.EquivalentTo(new[]
            {
                "manage-add-project-modal", "manage-add-follow-modal", "srv_cllen_modal", "srv_unsuball_modal",
                "srv_unfolall_modal"
            }));
            Assert.That(interactions.ComponentCommands.Select(c => c.Name),
                Is.SupersetOf(new[]
                {
                    "list:*:*:*:*", "manage-add-project:*", "manage-add-follow:*",
                    "follow-open:*:*:*", "follow_channel:*:*:*", "follow_newchannel:*:*:*", "follow_ping:*:*:*", "follow_newping:*:*:*",
                    "follow_events:*:*:*", "follow_release:*:*:*", "follow_loader:*:*:*",
                    "follow-remove:*:*:*", "follow-remove-confirm:*:*:*", "follow-remove-cancel:*:*:*",
                    "follow-ign:*:*:*:*:*", "follow-ign-t:*:*:*:*:*",
                    "entry-open:*:*:*", "entry_channel:*:*:*", "entry_ping:*:*:*", "release_filter:*:*:*",
                    "loader_filter:*:*:*", "entry-unsub:*:*:*", "entry-unsub-confirm:*:*:*", "entry-unsub-cancel:*:*:*",
                    "srv_manage:*", "srv_ping:*", "srv_lang:*", "srv_msgstyle:*", "srv_clstyle:*", "srv_subbtn:*",
                    "srv_cllen:*", "srv_preview:*", "srv_unsuball:*", "srv_unfolall:*",
                    "settings-main:*", "settings-module-more:*", "settings-module-notification-style:*",
                    "change-message-style:*", "change-changelog-style:*"
                }));
        }
    }
}
