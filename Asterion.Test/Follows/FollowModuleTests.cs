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
    public async Task ModulesBuildWithExpectedSlashCommands()
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
        var unfollow = await interactions.AddModuleAsync<UnfollowModule>(services);
        await interactions.AddModuleAsync<FollowInteractionModule>(services);
        await interactions.AddModuleAsync<EntryInteractionModule>(services);
        var manage = await interactions.AddModuleAsync<ManageInteractionModule>(services);
        await interactions.AddModuleAsync<ServerSettingsInteractionModule>(services);
        await interactions.AddModuleAsync<LegacySettingsInteractionModule>(services);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(follow.SlashGroupName, Is.EqualTo("follow"));
            Assert.That(follow.SlashCommands.Select(c => c.Name), Is.EquivalentTo(new[] { "user" }));
            Assert.That(unfollow.SlashGroupName, Is.EqualTo("unfollow"));
            Assert.That(unfollow.SlashCommands.Select(c => c.Name), Is.EquivalentTo(new[] { "user" }));
            Assert.That(manage.SlashCommands.Select(c => c.Name), Is.EquivalentTo(new[] { "manage" }));
        }
    }
}
