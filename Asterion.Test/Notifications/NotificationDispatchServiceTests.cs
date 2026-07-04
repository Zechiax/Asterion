using System.Text.Json;
using Asterion.Database;
using Asterion.Database.Models;
using Asterion.Services.Notifications;
using Asterion.Services.Notifications.Delivery;
using Asterion.Test.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modrinth;
using Modrinth.Models;
using Modrinth.Models.Enums.Project;
using NSubstitute;
using Array = Asterion.Database.Models.Array;
using Version = Modrinth.Models.Version;

namespace Asterion.Test.Notifications;

[TestFixture]
public class NotificationDispatchServiceTests
{
    private const ulong GuildId = 1;
    private const ulong EntryId = 1;
    private const string ProjectId = "project";

    private static async Task SeedAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var guild = new Guild { GuildId = GuildId, Created = DateTime.UtcNow };
        var settings = new GuildSettings { GuildId = GuildId, Guild = guild };
        var array = new Array { ArrayId = 1, GuildId = GuildId, Guild = guild, Type = ArrayType.Modrinth };
        var project = new ModrinthProject
            { ProjectId = ProjectId, LastCheckVersion = "v0", Created = DateTime.UtcNow };
        var entry = new ModrinthEntry
        {
            EntryId = EntryId,
            ArrayId = array.ArrayId,
            Array = array,
            ProjectId = ProjectId,
            GuildId = GuildId,
            Guild = guild,
            Created = DateTime.UtcNow,
            ReleaseFilter = ReleaseType.Alpha | ReleaseType.Beta | ReleaseType.Release,
            CustomUpdateChannel = 555
        };

        db.Guilds.Add(guild);
        db.GuildSettings.Add(settings);
        db.Arrays.Add(array);
        db.ModrinthProjects.Add(project);
        db.ModrinthEntries.Add(entry);
        await db.SaveChangesAsync();
    }

    private static async Task<int> SeedPendingNotificationAsync(IServiceProvider provider)
    {
        var project = new Project { Id = ProjectId, Title = "Test Project", Team = "team-1" };
        var version = new Version
        {
            Id = "v1", ProjectId = ProjectId, ProjectVersionType = ProjectVersionType.Release,
            VersionNumber = "1.0.0", Changelog = "Initial release", Loaders = ["fabric"],
            GameVersions = ["1.20.1"], Files = [], DatePublished = DateTime.UtcNow
        };

        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var row = new PendingNotification
        {
            ProjectId = ProjectId,
            VersionId = version.Id,
            SerializedProject = JsonSerializer.Serialize(project),
            SerializedVersion = JsonSerializer.Serialize(version),
            GuildId = GuildId,
            EntryId = EntryId,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.PendingNotifications.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private static NotificationDispatchService BuildService(IServiceProvider provider, RecordingNotificationSender sender)
    {
        var reader = new NotificationReader(provider);
        var signal = new NotificationSignal();
        var router = new NotificationSenderRouter([sender]);

        var modrinthClient = Substitute.For<IModrinthClient>();
        modrinthClient.Team.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(System.Array.Empty<TeamMember>());

        return new NotificationDispatchService(provider, reader, signal, router, modrinthClient,
            NullLogger<NotificationDispatchService>.Instance);
    }

    [Test]
    public async Task SuccessfulDispatchRecordsSendAndMarksRowSent()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedAsync(provider);
        var id = await SeedPendingNotificationAsync(provider);

        var sender = new RecordingNotificationSender();
        var service = BuildService(provider, sender);

        await service.ProcessDueNotificationsAsync();

        Assert.That(sender.Sent, Has.Count.EqualTo(1));
        Assert.That(sender.Sent[0].Target, Is.InstanceOf<BotDeliveryTarget>());

        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var row = await db.PendingNotifications.FindAsync(id);

        Assert.That(row!.Status, Is.EqualTo(NotificationStatus.Sent));
    }

    [Test]
    public async Task FailedSendSchedulesRetryOnTheSameRow()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        await SeedAsync(provider);
        var id = await SeedPendingNotificationAsync(provider);

        var sender = new RecordingNotificationSender { ThrowOnSend = new NotificationSendException("boom") };
        var service = BuildService(provider, sender);

        await service.ProcessDueNotificationsAsync();

        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var row = await db.PendingNotifications.FindAsync(id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row!.Status, Is.EqualTo(NotificationStatus.Failed));
            Assert.That(row.AttemptCount, Is.EqualTo(1));
            Assert.That(row.NextRetryAt, Is.Not.Null);
            Assert.That(await db.PendingNotifications.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task MissingEntryDeadLettersImmediately()
    {
        await using var provider = InMemoryDb.BuildServiceProvider();
        // No SeedAsync call - the entry the row references simply doesn't exist (unsubscribed).
        var id = await SeedPendingNotificationAsync(provider);

        var sender = new RecordingNotificationSender();
        var service = BuildService(provider, sender);

        await service.ProcessDueNotificationsAsync();

        Assert.That(sender.Sent, Is.Empty);

        using var scope = provider.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var row = await db.PendingNotifications.FindAsync(id);

        Assert.That(row!.Status, Is.EqualTo(NotificationStatus.DeadLettered));
    }
}
