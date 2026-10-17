### Asterion response strings — en-US (source language)

## Errors
error-modrinth-api-unavailable = The Modrinth API is currently unavailable
error-try-again-later = Please try again later
error-unknown = Unknown error

## Modrinth search
modrinth-search-unsuccessful = Search was not successful
modrinth-search-no-result = No results for query '{ $query }'

## Settings
settings = Settings
settings-button-notification-style = Notification Style
error-internal = Internal error, please try again later

## Follows
follow-event-project-update = Project updates
follow-event-new-project = New projects
follow-event-none = None
follow-added =
    Following **{ $name }** :white_check_mark: Tracking { $count ->
        [one] { $count } project
       *[other] { $count } projects
    }, messages will be sent to { $channel }.
follow-added-sync-failed = Following **{ $name }**, but the projects could not be loaded yet ({ $error }). I will try again soon.
follow-already-following = This server already follows **{ $name }**
follow-limit-reached = This server already follows the maximum of { $max } creators
follow-channel-invalid = There was an error processing your request, check if the channel is correct
follow-not-found = This server does not follow this creator
follow-removed = Stopped following **{ $name }** :white_check_mark:
follow-project-not-found = **{ $name }** has no project with ID { $project }
follow-project-ignored = Project **{ $project }** of **{ $name }** is now ignored :no_bell:
follow-project-unignored = Project **{ $project }** of **{ $name }** is no longer ignored :bell:
follow-list-empty = This server does not follow any creators. Use `/follow user` to follow a Modrinth user.
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
list-empty = You are not subscribed to any projects and do not follow any creators
list-page = Page { $page } of { $count }
list-tab-projects = Projects ({ $count })
list-tab-creators = Creators ({ $count })
list-subscriptions-title = Subscribed projects
list-subscriptions-description = Updates are sent for these projects
list-subscriptions-empty = You are not subscribed to any projects
list-subscription-entry = **{ $title }** ({ $id })
    In { $channel } since { $since }
entry-not-found = This server is not subscribed to this project
entry-unsub-confirm-question = Unsubscribe from **{ $title }** ({ $id })?
entry-unsubscribed = Unsubscribed from updates for **{ $title }** ({ $id }) :white_check_mark:
follow-remove-confirm-question = Stop following **{ $name }**?
no-send-permission = :warning: I don't have permission to send messages in { $channel }, please give me the required permissions :warning:

## Settings panel
settings-follow-title = Follow settings: { $name } ({ $type })
settings-subscription-title = Subscription settings: { $title } ({ $id })
settings-channel = Channel
settings-channel-placeholder = Select a channel
settings-ping-role = Ping role
settings-ping-role-placeholder = No role, the server's ping role is used
settings-new-project-ping-role = Ping role for new projects
settings-new-project-ping-role-placeholder = No role, the ping role is used
settings-events = Events
settings-events-placeholder = No events, nothing is sent
settings-release-filter = Release types
settings-release-placeholder = Select release type(s)
settings-loader-filter = Loaders
settings-loader-placeholder = All loaders
settings-none = None

## Buttons
button-settings = Settings
button-refresh = Refresh
button-unfollow = Unfollow
button-back = Back
button-unsubscribe = Unsubscribe
button-confirm = Confirm
button-cancel = Cancel
