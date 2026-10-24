# Commands

Options in `[brackets]` are optional. "Manage role" means Administrator or the role set in `/manage`.

## Modrinth

| Command | Options | Who can use it |
| --- | --- | --- |
| `/search` | `query` | Everyone |
| `/user` | `query` | Everyone |
| `/latest-release` | `slug-or-id` `[style]` | Everyone |
| `/random` | | Everyone |
| `/subscribe` | `project-id` `[custom-channel]` | Manage role |

- `/search`: search projects by slug, ID or search term and show the first result
- `/user`: show a Modrinth user, by ID or username
- `/latest-release`: show the latest release of a project, `style` is `Full` (default) or `Compact`
- `/random`: show a random project
- `/subscribe`: add a project to the watched list, updates go to `custom-channel` or the current channel

## Following

| Command | Options | Who can use it |
| --- | --- | --- |
| `/follow user` | `username` `[channel]` `[events]` `[new_project_channel]` | Manage role |

- `username`: username, ID or link of the Modrinth user
- `channel`: channel for the messages, the current channel if not set
- `events`: project updates, new projects and download milestones (default) / only project updates / only new projects
- `new_project_channel`: channel for new project messages, `channel` if not set

## Charts

| Command | Options | Who can use it |
| --- | --- | --- |
| `/chart downloads` | `project` `[range]` | Everyone |
| `/chart followers` | `project` `[range]` | Everyone |

- `project`: ID or slug, autocompletes from the server's subscribed projects
- `range`: 24 hours, 7 days, 30 days (default), 90 days, 1 year

## Management

| Command | Options | Who can use it |
| --- | --- | --- |
| `/manage` | | Manage role |

Subscribed projects, followed creators and server settings. See [The /manage panel](/guides/manage).

## Other

| Command | Options | Who can use it |
| --- | --- | --- |
| `/ping` | | Everyone |
| Find project on Modrinth | | Everyone |

- Find project on Modrinth: message command, right-click a message → Apps
