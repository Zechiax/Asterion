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
    public async Task FollowModulesBuildWithExpectedCommands()
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
        await interactions.AddModuleAsync<ListInteractionModule>(services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(follow.SlashGroupName, Is.EqualTo("follow"));
            Assert.That(follow.SlashCommands.Select(c => c.Name),
                Is.EquivalentTo(new[] { "user", "remove", "ignore", "unignore", "settings" }));
            Assert.That(interactions.ComponentCommands.Select(c => c.Name),
                Is.SupersetOf(new[]
                {
                    "list:*:*:*:*",
                    "follow-open:*:*:*", "follow_channel:*:*:*", "follow_ping:*:*:*", "follow_newping:*:*:*",
                    "follow_events:*:*:*", "follow_release:*:*:*", "follow_loader:*:*:*",
                    "follow-remove:*:*:*", "follow-remove-confirm:*:*:*", "follow-remove-cancel:*:*:*",
                    "entry-open:*:*:*", "entry_channel:*:*:*", "entry_ping:*:*:*", "release_filter:*:*:*",
                    "loader_filter:*:*:*", "entry-unsub:*:*:*", "entry-unsub-confirm:*:*:*", "entry-unsub-cancel:*:*:*"
                }));
        }
    }
}
