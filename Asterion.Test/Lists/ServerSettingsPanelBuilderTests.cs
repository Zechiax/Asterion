using Asterion.ComponentBuilders;
using Asterion.Database.Models;

namespace Asterion.Test.Lists;

[TestFixture]
public class ServerSettingsPanelBuilderTests
{
    [TestCase("unsubscribe", true)]
    [TestCase("  Unsubscribe ", true)]
    [TestCase("unsubscrib", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void DangerModalNeedsTheConfirmationWord(string? input, bool expected)
    {
        Assert.That(ServerSettingsPanelBuilder.IsConfirmed(input, "unsubscribe"), Is.EqualTo(expected));
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
