using System.Globalization;
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
        Assert.That(ServerSettingsPanelBuilder.IsConfirmed(input, "unsubscribe", null), Is.EqualTo(expected));
    }

    [Test]
    public void ConfirmationWordUsesItsOwnCultureNotTheThreads()
    {
        var threadCulture = CultureInfo.CurrentCulture;
        try
        {
            // Turkish casing maps "I" to the dotless "ı", so "UNFOLLOW" wouldn't match "unfollow" there
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.That(ServerSettingsPanelBuilder.IsConfirmed("UNFOLLOW", "unfollow",
                CultureInfo.GetCultureInfo("en-US")), Is.True);
        }
        finally
        {
            CultureInfo.CurrentCulture = threadCulture;
        }
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
