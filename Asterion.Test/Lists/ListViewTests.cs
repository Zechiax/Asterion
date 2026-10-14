using Asterion.Database.Models;
using Asterion.Interfaces;
using Asterion.Modules;
using Asterion.Services.Localization;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Asterion.Test.Lists;

[TestFixture]
public class ListViewTests
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
        return dataService;
    }

    private static List<ButtonComponent> Buttons(MessageComponent components)
    {
        return ComponentTree.Flatten(components).OfType<ButtonComponent>().ToList();
    }

    [Test]
    public async Task OpensOnProjects()
    {
        var (components, isEmpty) = await ListInteractionModule.BuildListAsync(MakeDataService(3),
            GuildId, Owner, null, 0, Loc, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isEmpty, Is.False);
            Assert.That(Buttons(components).Select(b => b.CustomId), Does.Contain("entry-open:42:p01:0"));
        }
    }

    [Test]
    public async Task EmptyWhenNothingIsSubscribed()
    {
        var (_, isEmpty) = await ListInteractionModule.BuildListAsync(MakeDataService(0),
            GuildId, Owner, null, 0, Loc, null);

        Assert.That(isEmpty, Is.True);
    }

    [Test]
    public async Task FullPageStaysWithinDiscordLimits()
    {
        var (components, _) = await ListInteractionModule.BuildListAsync(MakeDataService(25),
            GuildId, Owner, ListTab.Projects, 1, Loc, null, "Unsubscribed from something");

        var all = ComponentTree.Flatten(components);
        var ids = Buttons(components).Select(b => b.CustomId).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            Assert.That(ids, Is.Unique);
            Assert.That(all.OfType<ActionRowComponent>().Single().Components, Has.Count.EqualTo(3));
            // Settings buttons carry the shown page, so the panel can go back to it
            Assert.That(ids, Does.Contain("entry-open:42:p11:1"));
            Assert.That(all.OfType<TextDisplayComponent>().First().Content,
                Does.Contain("Unsubscribed from something"));
        }
    }

    [Test]
    public async Task ClampedPageIsUsedInSettingsButtons()
    {
        var (components, _) = await ListInteractionModule.BuildListAsync(MakeDataService(3),
            GuildId, Owner, ListTab.Projects, 5, Loc, null);

        Assert.That(Buttons(components).Select(b => b.CustomId), Does.Contain("entry-open:42:p01:0"));
    }

    [Test]
    public async Task EveryButtonCarriesTheOwnerForDoUserCheck()
    {
        var (components, _) = await ListInteractionModule.BuildListAsync(MakeDataService(25),
            GuildId, Owner, ListTab.Projects, 1, Loc, null);

        // DoUserCheck reads the user ID right after the first ':'
        Assert.That(Buttons(components).Select(b => b.CustomId.Split(':')[1]), Is.All.EqualTo("42"));
    }
}
