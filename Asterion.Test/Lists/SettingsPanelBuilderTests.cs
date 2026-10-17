using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
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

    private static ModrinthFollow MakeFollow()
    {
        return new ModrinthFollow
        {
            Id = 7,
            GuildId = 1,
            TargetType = FollowTargetType.User,
            TargetId = "user-id",
            TargetName = "Zechiax",
            Events = FollowEvents.ProjectUpdate | FollowEvents.NewProject,
            CustomUpdateChannel = 555,
            CustomPingRole = 666,
            LoaderFilter = ["fabric"],
            Created = DateTime.UtcNow
        };
    }

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

    private static ButtonBuilder Back()
    {
        return ManageInteractionModule.BackButton(42, ManageTab.Creators, 2, Loc, null);
    }

    private static List<SelectMenuComponent> Menus(MessageComponent panel)
    {
        return ComponentTree.Flatten(panel).OfType<SelectMenuComponent>().ToList();
    }

    [Test]
    public void FollowPanelHasAllSettingsWithinDiscordLimits()
    {
        var panel = FollowComponentBuilder.GetSettingsPanel(MakeFollow(), ["fabric", "quilt"], 42, 2, Back(), Loc,
            null);
        var all = ComponentTree.Flatten(panel);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            Assert.That(Menus(panel).Select(m => m.CustomId), Is.EqualTo(new[]
            {
                "follow_channel:42:7:2", "follow_newchannel:42:7:2", "follow_ping:42:7:2", "follow_newping:42:7:2", "follow_events:42:7:2", "follow_release:42:7:2",
                "follow_loader:42:7:2"
            }));
            Assert.That(all.OfType<ButtonComponent>().Select(b => b.CustomId),
                Is.EqualTo(new[] { "list:42:c:2:back", "follow-ign:42:7:2:0:open", "follow-remove:42:7:2" }));
            Assert.That(all.OfType<ButtonComponent>().Last().Label, Is.EqualTo("Unfollow"));
            Assert.That(all.OfType<TextDisplayComponent>().First().Content, Does.Contain("Zechiax (Modrinth user)"));
        }
    }

    [Test]
    public void PickersArePrefilledWithCurrentValues()
    {
        var menus = Menus(FollowComponentBuilder.GetSettingsPanel(MakeFollow(), [], 42, 2, Back(), Loc, null));

        var channel = menus.Single(m => m.CustomId == "follow_channel:42:7:2");
        var newProjectChannel = menus.Single(m => m.CustomId == "follow_newchannel:42:7:2");
        var pingRole = menus.Single(m => m.CustomId == "follow_ping:42:7:2");
        var newProjectRole = menus.Single(m => m.CustomId == "follow_newping:42:7:2");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(channel.Type, Is.EqualTo(ComponentType.ChannelSelect));
            Assert.That(channel.ChannelTypes, Is.EquivalentTo(new[] { ChannelType.Text, ChannelType.News }));
            Assert.That(channel.DefaultValues.Select(v => v.Id), Is.EqualTo(new[] { 555ul }));
            Assert.That(channel.MinValues, Is.EqualTo(1));

            // Can be deselected to use the update channel again
            Assert.That(newProjectChannel.Type, Is.EqualTo(ComponentType.ChannelSelect));
            Assert.That(newProjectChannel.MinValues, Is.EqualTo(0));
            Assert.That(newProjectChannel.DefaultValues, Is.Empty);

            Assert.That(pingRole.Type, Is.EqualTo(ComponentType.RoleSelect));
            Assert.That(pingRole.MinValues, Is.EqualTo(0));
            Assert.That(pingRole.DefaultValues.Select(v => v.Id), Is.EqualTo(new[] { 666ul }));

            Assert.That(newProjectRole.DefaultValues, Is.Empty);
        }
    }

    [Test]
    public void LoaderMenuKeepsCurrentFilterAndIsLeftOutWithoutLoaders()
    {
        var follow = MakeFollow();

        // The current filter stays selectable even if no project offers it
        var loaderMenu = Menus(FollowComponentBuilder.GetSettingsPanel(follow, [], 42, 2, Back(), Loc, null))
            .Single(m => m.CustomId == "follow_loader:42:7:2");
        Assert.That(loaderMenu.Options.Select(o => o.Value), Is.EqualTo(new[] { "fabric" }));

        follow.LoaderFilter = null;
        Assert.That(Menus(FollowComponentBuilder.GetSettingsPanel(follow, [], 42, 2, Back(), Loc, null))
            .Select(m => m.CustomId), Does.Not.Contain("follow_loader:42:7:2"));
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
    public void PanelShowsNotice()
    {
        var panel = FollowComponentBuilder.GetSettingsPanel(MakeFollow(), [], 42, 2, Back(), Loc, null,
            "Following Zechiax");

        Assert.That(ComponentTree.Flatten(panel).OfType<TextDisplayComponent>().First().Content,
            Does.Contain("Following Zechiax"));
    }

    [Test]
    public void IgnoredProjectsPageTogglesEachProject()
    {
        var follow = MakeFollow();
        follow.Projects =
        [
            new FollowProject
            {
                ProjectId = "AANobbMI", Ignored = true,
                Project = new ModrinthProject { ProjectId = "AANobbMI", Title = "Sodium", LastCheckVersion = "v" }
            },
            new FollowProject
            {
                ProjectId = "gvQqBUqZ",
                Project = new ModrinthProject { ProjectId = "gvQqBUqZ", Title = "Lithium", LastCheckVersion = "v" }
            }
        ];

        var page = FollowInteractionModule.BuildIgnoredProjectsPage(follow, 42, 2, 0, Loc, null);
        var all = ComponentTree.Flatten(page);
        var buttons = all.OfType<ButtonComponent>().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            // Sorted by title, the button does the opposite of the current state
            Assert.That(buttons.Take(2).Select(b => (b.CustomId, b.Label)), Is.EqualTo(new[]
            {
                ("follow-ign-t:42:7:2:0:gvQqBUqZ", "Ignore"),
                ("follow-ign-t:42:7:2:0:AANobbMI", "Unignore")
            }));
            Assert.That(buttons.Select(b => b.CustomId),
                Does.Contain("follow-ign:42:7:2:0:refresh").And.Contain("follow-open:42:7:2"));
            Assert.That(ComponentTree.Flatten(FollowComponentBuilder.GetSettingsPanel(follow, [], 42, 2, Back(), Loc,
                    null)).OfType<ButtonComponent>().Select(b => b.Label),
                Does.Contain("Projects (1 of 2 ignored)"));
        }
    }

    [Test]
    public void MessageWithoutButtonsHasNoActionRow()
    {
        var all = ComponentTree.Flatten(SettingsPanelBuilder.BuildMessage("Done", Color.Blue));

        Assert.That(all.OfType<ActionRowComponent>(), Is.Empty);
    }

    [Test]
    public void FollowListRowShowsTargetType()
    {
        var follow = MakeFollow();

        Assert.That(FollowEmbedBuilder.FormatListEntry(follow, Loc, null), Does.Contain("Type: Modrinth user"));
    }

    [TestCase(new string[0], null)]
    [TestCase(new[] { "123" }, 123ul)]
    [TestCase(new[] { "x" }, null)]
    public void ParsesSelectedId(string[] values, ulong? expected)
    {
        Assert.That(SettingsPanelBuilder.ParseSelectedId(values), Is.EqualTo(expected));
    }
}
