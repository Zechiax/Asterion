using Asterion.Database.Models;
using Asterion.Services.Notifications;

namespace Asterion.Test.Notifications;

[TestFixture]
public class DownloadMilestonesTests
{
    [TestCase(990, 999, MilestoneLadder.Standard, null)]
    [TestCase(999, 1_000, MilestoneLadder.Standard, 1_000L)]
    [TestCase(2_000, 2_600, MilestoneLadder.Standard, 2_500L)]
    [TestCase(900, 60_000, MilestoneLadder.Standard, 50_000L)]
    [TestCase(2_000, 60_000, MilestoneLadder.Round, 10_000L)]
    [TestCase(2_000, 60_000, MilestoneLadder.Major, null)]
    [TestCase(99_999, 100_000, MilestoneLadder.Major, 100_000L)]
    [TestCase(1_000, 1_000, MilestoneLadder.Standard, null)]
    [TestCase(1_200, 900, MilestoneLadder.Standard, null)]
    public void ReturnsHighestCrossedMilestone(long previous, long current, MilestoneLadder ladder, long? expected)
    {
        Assert.That(DownloadMilestones.HighestCrossed(previous, current, ladder), Is.EqualTo(expected));
    }
}
