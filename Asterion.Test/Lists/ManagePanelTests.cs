using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Asterion.Modules;
using Asterion.Services.Localization;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Asterion.Test.Lists;

[TestFixture]
public class ManagePanelTests
{
    private const ulong GuildId = 1;
    private const ulong Owner = 42;

    private static readonly ILocalizationService Loc = new LocalizationService(
        new FluentBundleStore(NullLogger<FluentBundleStore>.Instance), NullLogger<LocalizationService>.Instance);

    private static IDataService MakeDataService(int entryCount)
    {
        var entries = Enumerable.Range(1, entryCount).Select(i => new ModrinthEntry
        {
            EntryId = (ulong)i,
            ProjectId = $"p{i:D2}",
            GuildId = GuildId,
            Created = DateTime.UtcNow,
            Project = new ModrinthProject { ProjectId = $"p{i:D2}", Title = $"Project {i:D2}", LastCheckVersion = "v" }
        }).ToList();

        var dataService = Substitute.For<IDataService>();
        dataService.GetAllGuildsSubscribedProjectsAsync(GuildId).Returns(entries);
        dataService.GetGuildByIdAsync(GuildId).Returns(new Guild
        {
            GuildId = GuildId,
            ManageRole = 777,
            GuildSettings = new GuildSettings { GuildId = GuildId }
        });
        return dataService;
    }

    private static IFollowService MakeFollowService(int followCount)
    {
        var follows = Enumerable.Range(1, followCount).Select(i => new ModrinthFollow
        {
            Id = i,
            GuildId = GuildId,
            TargetId = $"u{i}",
            TargetName = $"user{i}",
            Created = DateTime.UtcNow
        }).ToList();

        var followService = Substitute.For<IFollowService>();
        followService.GetFollowsAsync(GuildId).Returns(follows);
        followService.CountFollowsAsync(GuildId).Returns(followCount);
        return followService;
    }

    private static Task<MessageComponent> Build(int entries, int follows, ManageTab? tab, int page,
        bool canManageServer = true, string? notice = null)
    {
        return ManageInteractionModule.BuildPanelAsync(MakeDataService(entries), MakeFollowService(follows), GuildId,
            Owner, tab, page, canManageServer, Loc, null, notice);
    }

    private static List<ButtonComponent> Buttons(MessageComponent components)
    {
        return ComponentTree.Flatten(components).OfType<ButtonComponent>().ToList();
    }

    private static List<ButtonComponent> Tabs(MessageComponent components)
    {
        return Buttons(components).Where(b => b.CustomId.EndsWith(":tab")).ToList();
    }

    [Test]
    public async Task ServerTabsAreOnlyForAdministrators()
    {
        var components = await Build(3, 0, ManageTab.General, 0, false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tabs(components).Select(b => b.IsDisabled), Is.EqualTo(new[] { true, false, true, true }));
            // Falls back to the projects
            Assert.That(Buttons(components).Select(b => b.CustomId), Does.Contain("entry-open:42:p01:0"));
        }
    }

    // Settings buttons carry the shown page (clamped), so the panel can go back to it
    [TestCase(1, "entry-open:42:p09:1")]
    [TestCase(99, "entry-open:42:p25:3")]
    public async Task SettingsButtonsCarryTheShownPage(int page, string expectedId)
    {
        var components = await Build(25, 0, ManageTab.Projects, page);

        Assert.That(Buttons(components).Select(b => b.CustomId), Does.Contain(expectedId));
    }

    [TestCase(ManageTab.Projects)]
    [TestCase(ManageTab.Creators)]
    [TestCase(ManageTab.General)]
    [TestCase(ManageTab.Notifications)]
    public async Task EveryComponentCarriesTheOwnerForDoUserCheck(ManageTab tab)
    {
        var components = await Build(25, 2, tab, 1, notice: "Unsubscribed from something");
        var all = ComponentTree.Flatten(components);
        var ids = all.OfType<ButtonComponent>().Select(b => b.CustomId)
            .Concat(all.OfType<SelectMenuComponent>().Select(m => m.CustomId))
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            // DoUserCheck reads the user ID right after the first ':'
            Assert.That(ids.Select(id => id.Split(':')[1]), Is.All.EqualTo("42"));
            Assert.That(ids, Is.Unique);
            Assert.That(ids.Select(id => id.Length), Is.All.LessThanOrEqualTo(100));
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            Assert.That(all.OfType<ActionRowComponent>().Select(r => r.Components.Count),
                Is.All.LessThanOrEqualTo(5));
        }
    }
}
