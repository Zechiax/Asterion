using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.Interfaces;
using Asterion.Services.Localization;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asterion.Test.Lists;

[TestFixture]
public class ServerSettingsPanelBuilderTests
{
    private static readonly ILocalizationService Loc = new LocalizationService(
        new FluentBundleStore(NullLogger<FluentBundleStore>.Instance), NullLogger<LocalizationService>.Instance);

    private static List<IMessageComponent> Flatten(ContainerBuilder container)
    {
        return ComponentTree.Flatten(new ComponentBuilderV2().AddComponent(container).Build());
    }

    [Test]
    public void NotificationsTabShowsCurrentStyles()
    {
        var settings = new GuildSettings
        {
            MessageStyle = MessageStyle.Compact,
            ChangelogStyle = ChangelogStyle.CodeBlock,
            ShowSubscribeButton = false,
            ChangeLogMaxLength = 1500
        };

        var all = Flatten(ServerSettingsPanelBuilder.BuildNotifications(settings, 42, Loc, null));
        var menus = all.OfType<SelectMenuComponent>().ToList();
        var buttons = all.OfType<ButtonComponent>().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(menus.Select(m => m.CustomId), Is.EqualTo(new[] { "srv_msgstyle:42", "srv_clstyle:42" }));
            Assert.That(menus[0].Options.Single(o => o.IsDefault == true).Label, Is.EqualTo("Compact"));
            Assert.That(menus[1].Options.Select(o => o.Label),
                Is.EqualTo(new[] { "Plain text", "Code block", "No changelog" }));
            Assert.That(menus[1].Options.Single(o => o.IsDefault == true).Value, Is.EqualTo("CodeBlock"));

            // Preview next to the title, then the setting's button next to its value
            Assert.That(buttons.Select(b => (b.CustomId, b.Label)), Is.EqualTo(new[]
            {
                ("srv_preview:42", "Preview"), ("srv_cllen:42", "Change")
            }));
            Assert.That(buttons[1].IsDisabled, Is.False);
            Assert.That(SettingText(all, "srv_cllen:42"), Is.EqualTo("**Changelog length**\n-# 1500 characters"));
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage - 5));
        }
    }

    [Test]
    public void ChangelogLengthShowsWhenItIsTheDefault()
    {
        var all = Flatten(ServerSettingsPanelBuilder.BuildNotifications(new GuildSettings(), 42, Loc, null));

        Assert.That(SettingText(all, "srv_cllen:42"), Does.EndWith("2000 characters (default)"));
    }

    [TestCase(true, "Shown under /search results", "Hide")]
    [TestCase(false, "Hidden under /search results", "Show")]
    public void GeneralTabTogglesTheSubscribeButton(bool shown, string state, string action)
    {
        var guild = new Guild { GuildSettings = new GuildSettings { ShowSubscribeButton = shown } };

        var all = Flatten(ServerSettingsPanelBuilder.BuildGeneral(guild, 42, 1, 1, ["en-US"], Loc, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SettingText(all, "srv_subbtn:42"), Is.EqualTo($"**Subscribe button**\n-# {state}"));
            Assert.That(all.OfType<ButtonComponent>().Single(b => b.CustomId == "srv_subbtn:42").Label,
                Is.EqualTo(action));
            Assert.That(all, Has.Count.LessThanOrEqualTo(ComponentTree.MaxComponentsPerMessage - 5));
        }
    }

    /// <summary>
    ///     Text of the setting row with the button next to it
    /// </summary>
    private static string SettingText(IEnumerable<IMessageComponent> all, string buttonId)
    {
        return all.OfType<SectionComponent>()
            .Single(s => s.Accessory is ButtonComponent b && b.CustomId == buttonId)
            .Components.OfType<TextDisplayComponent>().Single().Content;
    }

    [Test]
    public void ChangelogLengthIsDisabledWithoutChangelog()
    {
        var settings = new GuildSettings { ChangelogStyle = ChangelogStyle.NoChangelog };

        var buttons = Flatten(ServerSettingsPanelBuilder.BuildNotifications(settings, 42, Loc, null))
            .OfType<ButtonComponent>();

        Assert.That(buttons.Single(b => b.CustomId == "srv_cllen:42").IsDisabled, Is.True);
    }

    [Test]
    public void LanguageMenuMarksTheChosenLanguage()
    {
        var guild = new Guild { GuildSettings = new GuildSettings { Language = "en-US" } };

        var language = Flatten(ServerSettingsPanelBuilder.BuildGeneral(guild, 42, 1, 1, ["en-US"], Loc, null))
            .OfType<SelectMenuComponent>()
            .Single(m => m.CustomId == "srv_lang:42");

        Assert.That(language.Options.Single(o => o.IsDefault == true).Value, Is.EqualTo("en-US"));
    }

    [TestCase("unsubscribe", true)]
    [TestCase("  Unsubscribe ", true)]
    [TestCase("unsubscrib", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void DangerModalNeedsTheConfirmationWord(string? input, bool expected)
    {
        Assert.That(ServerSettingsPanelBuilder.IsConfirmed(input, "unsubscribe"), Is.EqualTo(expected));
    }

    [Test]
    public void DangerModalExplainsWhatIsRemoved()
    {
        var modal = ServerSettingsPanelBuilder.BuildDangerModal(ServerSettingsPanelBuilder.UnsubscribeAllModalId,
            "server-unsubscribe-all", 12, Loc, null);

        Assert.That(modal.CustomId, Is.EqualTo("srv_unsuball_modal"));
        Assert.That(modal.Title, Is.EqualTo("Unsubscribe from all projects"));
    }

    [TestCase("2000", 2000)]
    [TestCase(" 100 ", 100)]
    [TestCase("4000", 4000)]
    [TestCase("99", null)]
    [TestCase("4001", null)]
    [TestCase("-5", null)]
    [TestCase("abc", null)]
    // Left empty resets to the default
    [TestCase("", GuildSettings.DefaultChangeLogMaxLength)]
    [TestCase("  ", GuildSettings.DefaultChangeLogMaxLength)]
    [TestCase(null, GuildSettings.DefaultChangeLogMaxLength)]
    public void ParsesChangelogLength(string? input, int? expected)
    {
        Assert.That(ServerSettingsPanelBuilder.ParseChangelogLength(input), Is.EqualTo(expected));
    }
}
