### Asterion error and warning strings — en-US (source language)

## Generic
error-internal = Internal error, please try again later
error-unknown = Unknown error, please try again later
error-command-failed = An error occurred while executing this command. Please try again later, if the problem persists, please contact the bot owner.
error-invalid-input = The command's options are not valid, check them and try again
error-menu-outdated = This menu is outdated, use `/manage` to change the settings

## Modrinth
error-modrinth-api-unavailable = The Modrinth API is currently unavailable, please try again later
error-search-unsuccessful = Search was not successful
error-search-no-result = No results for query '{ $query }'
error-project-not-found = No project found for '{ $query }', check the slug or ID
error-no-more-results = Could not find any more projects
error-message-empty = The message has no text to search for
error-input-is-project = That is a link to a project, not to a user. Subscribe to the project instead, or use the link of the user's profile.

## Charts
error-no-stats-data = No statistics are stored for this project yet, it must be subscribed to in at least one server
error-not-enough-data = Not enough data for this chart yet, try again in about { $unit ->
    [hours] { $amount } hours
   *[days] { $amount } days
}

## Permissions
error-not-allowed = You can't use this here
error-guild-only = This can only be used in a server
error-manage-permission = For this, you either need the Administrator permission or the manage role of this server
error-not-panel-owner = Only the user who executed the command can use this
error-invalid-channel = There was an error processing your request, check if the channel is correct
error-channel-not-text = The channel must be a text channel
error-bot-cannot-access-channel = I can't see or send messages in { $channel }, please give me the required permissions

## Settings
error-changelog-length-invalid = Please enter a number from { $min } to { $max }, or leave it empty to reset to the default
error-danger-not-confirmed = You didn't type "{ $word }", nothing was removed

## Warnings
warning-bot-cannot-send = I don't have permission to send messages in { $channel }, please give me the required permissions
warning-already-subscribed = This server is already subscribed to this project
warning-already-unsubscribed = This server is not subscribed to this project
