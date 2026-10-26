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
| `/unsubscribe` | `project` | Manage role |

- `/search`: search projects by slug, ID or search term and show the first result
- `/user`: show a Modrinth user, by ID or username
- `/latest-release`: show the latest release of a project. `style` is `Full` or `Compact`, the server's
  [message style](/guides/manage#notifications) if not set.
- `/random`: show a random project
- `/subscribe`: add a project to the watched list, updates go to `custom-channel` or the current channel
- `/unsubscribe`: remove a project from the watched list after you confirm. `project` is an ID, slug or link and
  suggests the server's subscriptions.

## Following

| Command | Options | Who can use it |
| --- | --- | --- |
| `/follow user` | `username` `[channel]` `[events]` `[new_project_channel]` | Manage role |
| `/unfollow user` | `user` | Manage role |

- `username`: username, ID or link of the Modrinth user
- `channel`: channel for the messages, the current channel if not set
- `events`: project updates and new projects (default), only project updates, or only new projects
- `new_project_channel`: channel for new project messages, `channel` if not set
- `/unfollow user`: stop following a creator after you confirm. `user` is a username, ID or link and suggests the
  server's followed creators.

## Charts

| Command | Options | Who can use it |
| --- | --- | --- |
| `/chart downloads` | `project` `[range]` | Everyone |
| `/chart followers` | `project` `[range]` | Everyone |

- `project`: ID or slug. Administrators and the manage role get suggestions from the server's subscriptions.
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
