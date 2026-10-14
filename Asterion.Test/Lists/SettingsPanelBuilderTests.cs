using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Asterion.Modules;
using Asterion.Services.Localization;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using Modrinth;
using Modrinth.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Asterion.Test.Lists;

[TestFixture]
public class SettingsPanelBuilderTests
{
    private static readonly ILocalizationService Loc = new LocalizationService(
        new FluentBundleStore(NullLogger<FluentBundleStore>.Instance), NullLogger<LocalizationService>.Instance);

    private static ModrinthEntry MakeEntry()
    {
        return new ModrinthEntry
        {
            EntryId = 3,
            ProjectId = "AANobbMI",
            GuildId = 1,
            CustomUpdateChannel = 555,
            Created = DateTime.UtcNow
        };
    }

    private static IModrinthClient MakeClient(params string[] loaders)
    {
        var client = Substitute.For<IModrinthClient>();
        client.Project.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Project { Id = "AANobbMI", Title = "Sodium", Loaders = loaders });
        return client;
    }

    private static List<SelectMenuComponent> Menus(MessageComponent panel)
    {
        return ComponentTree.Flatten(panel).OfType<SelectMenuComponent>().ToList();
    }

    [Test]
    public async Task PanelOpenedByCommandHasNoBackButton()
    {
        var entryPanel = await EntryInteractionModule.BuildSettingsPanelAsync(MakeEntry(), 42, ListInteractionModule.NoList,
            Substitute.For<IDataService>(), MakeClient(), Loc, null);

        Assert.That(ComponentTree.Flatten(entryPanel).OfType<ButtonComponent>().Select(b => b.CustomId),
            Is.EqualTo(new[] { "entry-unsub:42:AANobbMI:-1" }));
    }

    [Test]
    public async Task PickersArePrefilledWithCurrentValues()
    {
        var entry = MakeEntry();
        entry.CustomPingRole = 666;
        var menus = Menus(await EntryInteractionModule.BuildSettingsPanelAsync(entry, 42, 2,
            Substitute.For<IDataService>(), MakeClient(), Loc, null));

        var channel = menus.Single(m => m.CustomId == "entry_channel:42:AANobbMI:2");
        var pingRole = menus.Single(m => m.CustomId == "entry_ping:42:AANobbMI:2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(channel.Type, Is.EqualTo(ComponentType.ChannelSelect));
            Assert.That(channel.ChannelTypes, Is.EquivalentTo(new[] { ChannelType.Text, ChannelType.News }));
            Assert.That(channel.DefaultValues.Select(v => v.Id), Is.EqualTo(new[] { 555ul }));

            Assert.That(pingRole.Type, Is.EqualTo(ComponentType.RoleSelect));
            Assert.That(pingRole.MinValues, Is.EqualTo(0));
            Assert.That(pingRole.DefaultValues.Select(v => v.Id), Is.EqualTo(new[] { 666ul }));
        }
    }

    [Test]
    public async Task SubscriptionPanelHasSharedSettings()
    {
        var dataService = Substitute.For<IDataService>();
        var panel = await EntryInteractionModule.BuildSettingsPanelAsync(MakeEntry(), 42, 2, dataService,
            MakeClient("fabric", "quilt"), Loc, null);
        var all = ComponentTree.Flatten(panel);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            Assert.That(Menus(panel).Select(m => m.CustomId), Is.EqualTo(new[]
            {
                "entry_channel:42:AANobbMI:2", "entry_ping:42:AANobbMI:2", "release_filter:42:AANobbMI:2", "loader_filter:42:AANobbMI:2"
            }));
            Assert.That(all.OfType<ButtonComponent>().Select(b => b.CustomId),
                Is.EqualTo(new[] { "list:42:p:2:back", "entry-unsub:42:AANobbMI:2" }));
            Assert.That(all.OfType<TextDisplayComponent>().First().Content, Does.Contain("Sodium (AANobbMI)"));
        }
    }

    [Test]
    public async Task SubscriptionPanelWorksWithoutModrinthApi()
    {
        var client = Substitute.For<IModrinthClient>();
        client.Project.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("down"));
        var dataService = Substitute.For<IDataService>();
        dataService.GetModrinthProjectByIdAsync("AANobbMI")
            .Returns(new ModrinthProject { ProjectId = "AANobbMI", Title = "Sodium", LastCheckVersion = "v" });

        var panel = await EntryInteractionModule.BuildSettingsPanelAsync(MakeEntry(), 42, 2, dataService, client, Loc, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Menus(panel).Select(m => m.CustomId), Does.Not.Contain("loader_filter:42:AANobbMI:2"));
            Assert.That(ComponentTree.Flatten(panel).OfType<TextDisplayComponent>().First().Content,
                Does.Contain("Sodium"));
        }
    }

    [Test]
    public void MessageWithoutButtonsHasNoActionRow()
    {
        var all = ComponentTree.Flatten(SettingsPanelBuilder.BuildMessage("Done", Color.Blue));

        Assert.That(all.OfType<ActionRowComponent>(), Is.Empty);
    }

    [TestCase(new string[0], null)]
    [TestCase(new[] { "123" }, 123ul)]
    [TestCase(new[] { "x" }, null)]
    public void ParsesSelectedId(string[] values, ulong? expected)
    {
        Assert.That(SettingsPanelBuilder.ParseSelectedId(values), Is.EqualTo(expected));
    }
}
