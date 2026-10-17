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
    public async Task OpensOnProjectsWithTabsForEverything()
    {
        var components = await Build(3, 2, null, 0);
        var tabs = Tabs(components);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tabs.Select(b => b.Label),
                Is.EqualTo(new[] { "Projects (3)", "Creators (2)", "General", "Notifications" }));
            Assert.That(tabs.Select(b => b.IsDisabled), Is.EqualTo(new[] { true, false, false, false }));
            Assert.That(tabs.Select(b => b.CustomId),
                Is.EqualTo(new[] { "list:42:p:0:tab", "list:42:c:0:tab", "list:42:g:0:tab", "list:42:n:0:tab" }));
            Assert.That(Buttons(components).Select(b => b.CustomId),
                Does.Contain("entry-open:42:p01:0").And.Contain("manage-add-project:42"));
        }
    }

    [Test]
    public async Task OpensOnCreatorsWithoutProjects()
    {
        var components = await Build(0, 2, null, 0);

        Assert.That(Buttons(components).Select(b => b.CustomId),
            Does.Contain("follow-open:42:1:0").And.Contain("manage-add-follow:42"));
    }

    [Test]
    public async Task EmptyServerShowsProjectsWithAddButton()
    {
        var components = await Build(0, 0, null, 0);

        Assert.That(Buttons(components).Select(b => b.CustomId), Does.Contain("manage-add-project:42"));
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

    [Test]
    public async Task FullPageWithTabsStaysWithinDiscordLimits()
    {
        var components = await Build(25, 0, ManageTab.Projects, 1, notice: "Unsubscribed from something");

        var all = ComponentTree.Flatten(components);
        var ids = Buttons(components).Select(b => b.CustomId).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            Assert.That(ids, Is.Unique);
            Assert.That(all.OfType<ActionRowComponent>().Select(r => r.Components.Count),
                Is.EqualTo(new[] { 4, 4 }));
            // Settings buttons carry the shown page, so the panel can go back to it
            Assert.That(ids, Does.Contain("entry-open:42:p09:1"));
            Assert.That(all.OfType<TextDisplayComponent>().First().Content,
                Does.Contain("Unsubscribed from something"));
        }
    }

    [Test]
    public async Task ClampedPageIsUsedInSettingsButtons()
    {
        var components = await Build(3, 0, ManageTab.Projects, 5);

        Assert.That(Buttons(components).Select(b => b.CustomId), Does.Contain("entry-open:42:p01:0"));
    }

    [TestCase(ManageTab.Projects)]
    [TestCase(ManageTab.Creators)]
    [TestCase(ManageTab.General)]
    [TestCase(ManageTab.Notifications)]
    public async Task EveryComponentCarriesTheOwnerForDoUserCheck(ManageTab tab)
    {
        var components = await Build(25, 2, tab, 1);
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
        }
    }

    [Test]
    public async Task GeneralTabShowsServerSettings()
    {
        var components = await Build(0, 2, ManageTab.General, 0);
        var all = ComponentTree.Flatten(components);
        var menus = all.OfType<SelectMenuComponent>().ToList();
        var buttons = Buttons(components);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(menus.Select(m => m.CustomId),
                Is.EqualTo(new[] { "srv_manage:42", "srv_ping:42", "srv_lang:42" }));
            Assert.That(menus[0].DefaultValues.Select(v => v.Id), Is.EqualTo(new[] { 777ul }));
            Assert.That(menus[1].DefaultValues, Is.Empty);
            // No language chosen, Discord's language is used
            Assert.That(menus[2].Options.Single(o => o.IsDefault == true).Value,
                Is.EqualTo(ServerSettingsPanelBuilder.DefaultLanguageValue));
            Assert.That(menus[2].Options.Select(o => o.Value), Does.Contain(FluentBundleStore.DefaultLocale));
            // Nothing to unsubscribe from, but there are follows
            Assert.That(buttons.Single(b => b.CustomId == "srv_unsuball:42").IsDisabled, Is.True);
            Assert.That(buttons.Single(b => b.CustomId == "srv_unfolall:42").IsDisabled, Is.False);
        }
    }
}
