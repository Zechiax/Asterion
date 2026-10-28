### Asterion response strings — en-US (source language)

## Follows
follow-event-project-update = Project updates
follow-event-new-project = New projects
follow-event-download-milestone = Project download milestones
follow-event-project-update-description = New versions of the creator's projects
follow-event-new-project-description = Projects the creator publishes
follow-event-download-milestone-description = When one of the creator's projects reaches a milestone
follow-event-none = None
follow-added =
    Following **{ $name }** :white_check_mark: Tracking { $count ->
        [one] { $count } project
       *[other] { $count } projects
    }, messages will be sent to { $channel }.
follow-added-sync-failed = Following **{ $name }**, but the projects could not be loaded yet ({ $error }). I will try again soon.
follow-already-following = This server already follows **{ $name }**
follow-limit-reached = This server already follows the maximum of { $max } creators
follow-not-found = This server does not follow this creator
follow-removed = Stopped following **{ $name }** :white_check_mark:
follow-project-not-found = **{ $name }** has no project with ID { $project }
follow-list-empty = This server does not follow any creators. Use **Follow creator** to follow a Modrinth user.
follow-list-title = Followed creators
follow-list-description = Updates are sent for all projects of these creators
follow-list-entry =
    Type: { $type }
    Events: { $events }
    Channel: { $channel }
    Projects: { $projects } ({ $ignored } ignored)
follow-target-user = Modrinth user
follow-target-organization = Modrinth organization
follow-button-follow-user = Follow user

## Lists
list-page = Page { $page } of { $count }
list-tab-projects = Projects ({ $count })
list-tab-creators = Creators ({ $count })
list-subscriptions-title = Subscribed projects
list-subscriptions-description = Updates are sent for these projects
list-subscriptions-empty = You are not subscribed to any projects. Use **Add project** to subscribe to one.
list-subscription-entry = **{ $title }** ({ $id })
    In { $channel } since { $since }
entry-not-found = This server is not subscribed to this project
entry-unsub-confirm-question = Unsubscribe from **{ $title }** ({ $id })?
entry-unsubscribed = Unsubscribed from updates for **{ $title }** ({ $id }) :white_check_mark:
follow-remove-confirm-question = Stop following **{ $name }**?

## Settings panel
settings-follow-title = Follow settings: { $name } ({ $type })
settings-subscription-title = Subscription settings: { $title } ({ $id })
settings-channel = Channel
settings-channel-placeholder = Select a channel
settings-new-project-channel = New projects channel override
settings-new-project-channel-placeholder = Same as the channel above
settings-ping-role = Ping role override
settings-ping-role-placeholder = Default ping role
settings-new-project-ping-role = New projects ping role override
settings-new-project-ping-role-placeholder = Same as the ping role above
settings-milestone-channel = Milestone channel override
settings-milestone-channel-placeholder = Server's milestone channel
settings-events = Events
settings-events-placeholder = No events
settings-release-filter = Release types
settings-release-placeholder = Select release types
settings-loader-filter = Loaders
settings-loader-placeholder = All loaders
settings-none = None

## Manage panel
manage-tab-general = General
manage-tab-notifications = Notifications
manage-add-project-title = Add project
manage-add-project-input = Project
manage-add-project-placeholder = Slug, ID or link, e.g. sodium
manage-add-follow-title = Follow creator
manage-add-follow-input = Modrinth user
manage-add-follow-placeholder = Username, ID or link
manage-subscribed = Subscribed to **{ $title }** :white_check_mark: Updates will be sent to { $channel }.
manage-already-subscribed = This server is already subscribed to **{ $title }**
follow-projects-title = Projects of { $name }
follow-projects-description = No messages are sent for ignored projects
follow-projects-empty = No projects are known yet, they are loaded with the next sync
follow-project-entry = **{ $title }** ({ $id })
    :bell: Messages are sent
follow-project-entry-ignored = **{ $title }** ({ $id })
    :no_bell: Ignored

## Server settings
server-general-title = Server settings
server-general-description = Who can manage Asterion, who gets pinged and which language it speaks
server-manage-role = Manage role
server-manage-role-placeholder = Only administrators
server-ping-role = Default ping role
server-ping-role-placeholder = Nobody is pinged
server-language = Language
server-language-default = Discord's language of the server
server-danger-zone = Danger zone
server-unsubscribe-all = Unsubscribe from all projects
server-unfollow-all = Unfollow all creators
server-unsubscribe-all-title = Unsubscribe from all projects
server-unsubscribe-all-warning =
    ## :warning: This removes all { $count } subscriptions
    No more update messages are sent for any subscribed project, and the settings of every subscription (channel, ping role, filters) are deleted. This can't be undone.
server-unsubscribe-all-word = unsubscribe
server-unfollow-all-title = Unfollow all creators
server-unfollow-all-warning =
    ## :warning: This removes all { $count } follows
    No more messages are sent for any followed creator, and the settings of every follow (channels, ping roles, ignored projects, filters) are deleted. This can't be undone.
server-unfollow-all-word = unfollow
server-danger-confirm-input = Type "{ $word }" to confirm
server-unsubscribed-all = Unsubscribed from { $count } projects :white_check_mark:
server-unfollowed-all = Stopped following { $count } creators :white_check_mark:
server-notifications-title = Update messages
server-notifications-description = How the messages about new versions look
server-message-style = Message style
server-changelog-style = Changelog style
server-search-title = Search
server-subscribe-button = Subscribe button
server-subscribe-button-shown = Shown under /search results
server-subscribe-button-hidden = Hidden under /search results
server-changelog-length = Changelog length
server-changelog-length-value = { $length } characters
server-changelog-length-value-default = { $length } characters (default)
server-changelog-length-title = Changelog length
server-changelog-length-input = Maximum number of characters
server-changelog-length-hint = From { $min } to { $max }. Leave empty to reset to the default ({ $default }).
message-style-full = Full
message-style-compact = Compact
changelog-style-plaintext = Plain text
changelog-style-codeblock = Code block
changelog-style-nochangelog = No changelog
server-milestones-title = Download milestones
server-milestones = Milestone messages
server-milestones-enabled = Sent when a project reaches a download milestone
server-milestones-disabled = Not sent
server-milestones-override-hint = The channel below applies to all projects. Each subscribed project and followed creator can override it in its own settings.
server-milestone-ladder = Milestones
server-milestone-channel = Milestone channel
server-milestone-channel-placeholder = Channel of each subscription or follow
server-milestone-ping-role = Milestone ping role
server-milestone-ping-role-placeholder = Nobody is pinged
milestone-ladder-standard = Standard: 1k, 2.5k, 5k, 10k, 25k…
milestone-ladder-round = Round: 1k, 10k, 100k, 1M…
milestone-ladder-major = Major: 100k, 1M, 10M…

## Buttons
button-add-project = Add project
button-follow-creator = Follow creator
button-preview = Preview
button-change = Change
button-hide = Hide
button-show = Show
button-enable = Enable
button-disable = Disable
button-milestones-on = Milestones: on
button-milestones-off = Milestones: off
button-milestones-server-off = Milestones: off on this server
button-ignore = Ignore
button-unignore = Unignore
button-ignored-projects = Projects ({ $ignored } of { $count } ignored)
button-settings = Settings
button-refresh = Refresh
button-unfollow = Unfollow
button-back = Back
button-unsubscribe = Unsubscribe
button-confirm = Confirm
button-cancel = Cancel
removal-cancelled = Cancelled, nothing was removed

## Charts
chart-caption-downloads = Downloads of **{ $title }** per { $range ->
    [day] hour over the last 24 hours
    [week] 6 hours over the last 7 days
    [quarter] day over the last 90 days
    [year] week over the last year
   *[month] day over the last 30 days
} (UTC)
chart-caption-followers = New followers of **{ $title }** per { $range ->
    [day] hour over the last 24 hours
    [week] 6 hours over the last 7 days
    [quarter] day over the last 90 days
    [year] week over the last year
   *[month] day over the last 30 days
} (UTC)
