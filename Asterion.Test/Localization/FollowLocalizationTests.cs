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
        "follow-project-not-found",
        "follow-list-empty", "follow-list-title", "follow-list-description", "follow-list-entry",
        "follow-button-follow-user", "follow-target-user", "follow-target-organization",
        "settings-follow-title", "settings-subscription-title", "settings-channel", "settings-channel-placeholder",
        "settings-ping-role", "settings-ping-role-placeholder", "settings-new-project-ping-role",
        "settings-new-project-ping-role-placeholder", "settings-events", "settings-events-placeholder",
        "settings-release-filter", "settings-release-placeholder", "settings-loader-filter",
        "settings-loader-placeholder", "settings-none", "follow-remove-confirm-question",
        "list-page", "list-subscriptions-title", "list-subscriptions-description",
        "list-subscriptions-empty", "list-subscription-entry",
        "entry-not-found", "entry-unsub-confirm-question", "entry-unsubscribed",
        "button-settings", "button-refresh", "button-unfollow", "button-back", "list-tab-projects", "list-tab-creators", "button-unsubscribe", "button-confirm",
        "button-cancel",
        "settings-outdated", "manage-tab-general", "manage-tab-notifications", "manage-add-project-title",
        "manage-add-project-input", "manage-add-project-placeholder", "manage-add-follow-title",
        "manage-add-follow-input", "manage-add-follow-placeholder", "manage-subscribed", "manage-already-subscribed",
        "manage-project-not-found", "follow-projects-title", "follow-projects-description", "follow-projects-empty",
        "follow-project-entry", "follow-project-entry-ignored",
        "server-general-title", "server-general-description", "server-manage-role", "server-manage-role-placeholder",
        "server-ping-role", "server-ping-role-placeholder", "server-language", "server-language-default",
        "server-danger-zone", "server-unsubscribe-all", "server-unfollow-all", "server-unsubscribe-all-title",
        "server-unsubscribe-all-warning", "server-unsubscribe-all-word", "server-unfollow-all-title",
        "server-unfollow-all-warning", "server-unfollow-all-word", "server-danger-confirm-input",
        "server-danger-not-confirmed", "server-unsubscribed-all", "server-unfollowed-all",
        "settings-new-project-channel", "settings-new-project-channel-placeholder",
        "server-notifications-title", "server-notifications-description", "server-message-style",
        "server-changelog-style", "server-subscribe-button-shown", "server-subscribe-button-hidden",
        "server-changelog-length", "server-changelog-length-value", "server-changelog-length-value-default",
        "server-changelog-length-title", "server-search-title", "server-subscribe-button", "button-change",
        "button-hide", "button-show",
        "server-changelog-length-input", "server-changelog-length-hint",
        "server-changelog-length-invalid", "message-style-full", "message-style-compact",
        "changelog-style-plaintext", "changelog-style-codeblock", "changelog-style-nochangelog",
        "button-add-project", "button-follow-creator", "button-preview", "button-ignore", "button-unignore",
        "button-ignored-projects"
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
            events = "events", projects = 3, ignored = 1, type = "type", query = "query", length = 2000, min = 100, word = "word",
            @default = 2000
        };

        Assert.That(BuildService().Get(key, null, args), Is.Not.EqualTo(key));
    }

    [Test]
    public void FollowCommandKeysResolve()
    {
        var store = new FluentBundleStore(NullLogger<FluentBundleStore>.Instance);

        foreach (var command in new[] { "follow", "follow-user", "manage" })
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
