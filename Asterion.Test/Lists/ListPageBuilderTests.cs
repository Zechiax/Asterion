using Asterion.ComponentBuilders;
using Asterion.Interfaces;
using Asterion.Services.Localization;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asterion.Test.Lists;

[TestFixture]
public class ListPageBuilderTests
{
    private static readonly ILocalizationService Loc = new LocalizationService(
        new FluentBundleStore(NullLogger<FluentBundleStore>.Instance), NullLogger<LocalizationService>.Instance);

    private static (MessageComponent Components, int Page) Build(int count, int page)
    {
        var items = Enumerable.Range(1, count).ToList();

        return ListPageBuilder.Build(items, page, "Title", "Description", Color.Blue, "test-page",
            i => $"**Item {i}**\n" + new string('x', 1000),
            i => new ButtonBuilder("Settings", $"test-open:{i}", ButtonStyle.Secondary),
            "Nothing here", Loc, null);
    }

    private static List<IMessageComponent> FlattenPage(MessageComponent components)
    {
        return ComponentTree.Flatten(components);
    }

    [Test]
    public void FullPageStaysWithinDiscordLimits()
    {
        var all = FlattenPage(Build(25, 0).Components);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            Assert.That(all.OfType<TextDisplayComponent>().Sum(t => t.Content.Length), Is.LessThanOrEqualTo(4000));
        }
    }

    [Test]
    public void SplitsRowsIntoPages()
    {
        Assert.That(ListPageBuilder.GetPageCount(25), Is.EqualTo(4));

        var sections = Enumerable.Range(0, 4)
            .Select(page => FlattenPage(Build(25, page).Components).OfType<SectionComponent>().Count());

        Assert.That(sections, Is.EqualTo(new[] { 8, 8, 8, 1 }));
    }

    [TestCase(-1, 0)]
    [TestCase(99, 3)]
    public void ClampsPage(int requested, int expected)
    {
        Assert.That(Build(25, requested).Page, Is.EqualTo(expected));
    }

    [Test]
    public void NavigationButtonsPointToNeighbourPages()
    {
        var buttons = FlattenPage(Build(25, 0).Components).OfType<ButtonComponent>()
            .Where(b => b.CustomId.StartsWith("test-page:"))
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(buttons.Select(b => b.CustomId),
                Is.EqualTo(new[] { "test-page:0:prev", "test-page:0:refresh", "test-page:1:next" }));
            Assert.That(buttons.Select(b => b.IsDisabled), Is.EqualTo(new[] { true, false, false }));
        }

        var last = FlattenPage(Build(25, 3).Components).OfType<ButtonComponent>()
            .Single(b => b.CustomId.EndsWith(":next"));
        Assert.That(last.IsDisabled, Is.True);
    }

    [Test]
    public void FullPageWithTabsAndExtraButtonsStaysWithinDiscordLimits()
    {
        var items = Enumerable.Range(1, 25).ToList();
        var tabs = Enumerable.Range(1, 4).Select(i => new ButtonBuilder($"Tab {i}", $"test-tab:{i}"));
        var extra = Enumerable.Range(1, 2).Select(i => new ButtonBuilder($"Extra {i}", $"test-extra:{i}"));

        var components = ListPageBuilder.Build(items, 1, "Title", "Description", Color.Blue, "test-page",
            i => $"**Item {i}**", i => new ButtonBuilder("Settings", $"test-open:{i}", ButtonStyle.Secondary),
            "Nothing here", Loc, null, "Notice", extra, tabs).Components;
        var all = FlattenPage(components);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage));
            // Tabs on top, navigation at the bottom
            Assert.That(components.Components.First(), Is.InstanceOf<ActionRowComponent>());
            Assert.That(((ActionRowComponent)components.Components.First()).Components, Has.Count.EqualTo(4));
            Assert.That(((ActionRowComponent)components.Components.Last()).Components, Has.Count.EqualTo(5));
        }
    }

    [Test]
    public void AllCustomIdsAreUnique()
    {
        var ids = FlattenPage(Build(25, 1).Components).OfType<ButtonComponent>().Select(b => b.CustomId).ToList();

        Assert.That(ids, Is.Unique);
    }

    [Test]
    public void SinglePageHasOnlyRefreshButton()
    {
        var all = FlattenPage(Build(3, 0).Components);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.OfType<SectionComponent>().Count(), Is.EqualTo(3));
            Assert.That(all.OfType<ButtonComponent>().Where(b => b.CustomId.StartsWith("test-page:"))
                .Select(b => b.CustomId), Is.EqualTo(new[] { "test-page:0:refresh" }));
            Assert.That(all.OfType<TextDisplayComponent>().First().Content, Does.Not.Contain("Page"));
        }
    }

    [Test]
    public void EmptyListShowsEmptyText()
    {
        var all = FlattenPage(Build(0, 0).Components);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all.OfType<SectionComponent>(), Is.Empty);
            Assert.That(all.OfType<TextDisplayComponent>().Select(t => t.Content), Does.Contain("Nothing here"));
        }
    }
}
