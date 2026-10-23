using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modrinth.Models;

namespace Asterion.Test.Statistics;

[TestFixture]
public class ProjectStatisticsManagerTests
{
    // Polls within the same hour overwrite the hour's and the day's row, so they end up with the last sample
    [Test]
    public async Task RecordOverwritesSamplesOfTheSameHourAndDay()
    {
        var name = Guid.NewGuid().ToString();
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<DataContext>(options => options.UseInMemoryDatabase(name))
            .BuildServiceProvider();

        await using (var db = provider.CreateScope().ServiceProvider.GetRequiredService<DataContext>())
        {
            db.ModrinthProjects.Add(new ModrinthProject
                { ProjectId = "AANobbMI", LastCheckVersion = "v1", Created = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var manager = new ProjectStatisticsManager(provider);
        await manager.RecordAsync([new Project { Id = "AANobbMI", Downloads = 100, Followers = 5 }]);
        await manager.RecordAsync([
            new Project { Id = "AANobbMI", Downloads = 120, Followers = 6 },
            new Project { Id = "unknown", Downloads = 1, Followers = 1 }
        ]);

        await using (var db = provider.CreateScope().ServiceProvider.GetRequiredService<DataContext>())
        {
            var hourly = await db.HourlyProjectStats.SingleAsync();
            var daily = await db.DailyProjectStats.SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That((hourly.ProjectId, hourly.Downloads, hourly.Followers), Is.EqualTo(("AANobbMI", 120, 6)));
                Assert.That((daily.ProjectId, daily.Downloads, daily.Followers), Is.EqualTo(("AANobbMI", 120, 6)));
                Assert.That(hourly.Hour.Minute, Is.Zero);
                Assert.That(daily.Day, Is.EqualTo(daily.Day.Date));
            });
        }
    }
}
