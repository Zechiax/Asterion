using Asterion.ComponentBuilders;
using Asterion.Database.Models;
using Asterion.EmbedBuilders;
using Asterion.Interfaces;
using Asterion.Services.Localization;
using Discord;
using Linguini.Bundle;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asterion.Test.Localization;

[TestFixture]
public class FollowLocalizationTests
{
    private static readonly string[] Keys =
    [
        "error-internal",
        "follow-event-project-update", "follow-event-new-project", "follow-event-none",
        "follow-added", "follow-added-sync-failed", "follow-already-following", "follow-limit-reached",
        "follow-channel-invalid", "no-send-permission", "follow-not-found", "follow-removed",
        "follow-project-not-found", "follow-project-ignored", "follow-project-unignored",
        "follow-list-empty", "follow-list-title", "follow-list-description", "follow-list-entry",
        "follow-button-follow-user", "follow-target-user", "follow-target-organization",
        "settings-follow-title", "settings-subscription-title", "settings-channel", "settings-channel-placeholder",
        "settings-ping-role", "settings-ping-role-placeholder", "settings-new-project-ping-role",
        "settings-new-project-ping-role-placeholder", "settings-events", "settings-events-placeholder",
        "settings-release-filter", "settings-release-placeholder", "settings-loader-filter",
        "settings-loader-placeholder", "settings-none", "follow-remove-confirm-question",
        "list-empty", "list-page", "list-subscriptions-title", "list-subscriptions-description",
        "list-subscriptions-empty", "list-subscription-entry",
        "entry-not-found", "entry-unsub-confirm-question", "entry-unsubscribed",
        "button-settings", "button-refresh", "button-unfollow", "button-back", "list-tab-projects", "list-tab-creators", "button-unsubscribe", "button-confirm",
        "button-cancel"
    ];

    private static LocalizationService BuildService()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);
        return new LocalizationService(store, NullLogger<LocalizationService>.Instance);
    }

    [TestCaseSource(nameof(Keys))]
    public void FollowKeyResolves(string key)
    {
        var args = new
        {
            name = "name", count = 2, page = 1, title = "title", id = "id", since = "today", channel = "#channel", error = "error", max = 10, project = "project",
            events = "events", projects = 3, ignored = 1, type = "type"
        };

        Assert.That(BuildService().Get(key, null, args), Is.Not.EqualTo(key));
    }

    [Test]
    public void FollowCommandKeysResolve()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);

        foreach (var command in new[] { "follow", "follow-user", "follow-remove", "follow-ignore",
                     "follow-unignore", "follow-settings" })
            Assert.That(store.Default.TryGetMessage($"command-{command}", "description", null, out _, out _),
                Is.True, command);
    }

    [Test]
    public void AddedMessageUsesPluralForProjectCount()
    {
        var service = BuildService();
        var result = new AddFollowResult(AddFollowStatus.Added, Sync: new SyncResult(true, null, 1, [], [], []));

        var text = FollowEmbedBuilder.DescribeAddResult(result, "jellysquid", "#updates", service, null);

        Assert.That(text, Does.Contain("1 project,").And.Contain("jellysquid").And.Contain("#updates"));
    }

    [Test]
    public void FormatsEvents()
    {
        var service = BuildService();

        Assert.That(FollowEmbedBuilder.FormatEvents(FollowEvents.ProjectUpdate | FollowEvents.NewProject, service, null),
            Is.EqualTo("Project updates, New projects"));
        Assert.That(FollowEmbedBuilder.FormatEvents(FollowEvents.None, service, null), Is.EqualTo("None"));
    }
}
