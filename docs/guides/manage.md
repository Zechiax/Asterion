# The /manage panel

`/manage` is where you change subscriptions, follows and server settings.

## Who can use it

- **Projects** and **Creators** tabs: Administrators and members with the manage role.
- **General** and **Notifications** tabs: Administrators only. For everyone else these tabs are greyed out.

The panel is only visible to you. If someone else clicks its buttons, they get "Only the user who executed the
command can use this". They can run `/manage` themselves.

## Projects

Lists the projects this server is subscribed to.

![Projects tab of /manage](/manage-projects.png)

- **Settings** opens the settings of one subscription. See [Change a subscription](/guides/subscriptions#change-a-subscription).
- **Add project** asks for the project (slug, ID or link) and the channel.

## Creators

Lists the Modrinth users this server follows, with their channel, events and how many of their projects are ignored.

![Creators tab of /manage](/manage-creators.png)

- **Settings** opens the settings of one follow. See [Change a follow](/guides/following#change-a-follow).
- **Follow creator** asks for the user, the channel, an optional new projects channel and the events.

## General

![General tab of /manage](/manage-general.png)

| Setting | What it does |
| --- | --- |
| **Manage role** | Members with this role can subscribe, follow and use the **Projects** and **Creators** tabs. Empty means only Administrators. |
| **Default ping role** | Pinged on every update. Each subscription or follow can use another role instead. Empty means nobody is pinged. |
| **Language** | Language of the bot's messages. By default it follows the server's language in Discord. English is the only language for now. |
| **Subscribe button** | Show or hide the **Subscribe** button under `/search` results. `/random` always shows it. |

### Remove everything

**Unsubscribe from all projects** and **Unfollow all creators** remove all subscriptions or all follows of this
server. To confirm, type `unsubscribe` or `unfollow` in the box that opens. This can't be undone.

## Notifications

These settings change how update messages look. New project messages always look the same.

![Notifications tab of /manage](/manage-notifications.png)

| Setting | Options |
| --- | --- |
| **Message style** | **Full** (default): changelog, Minecraft versions, loaders, files and links. **Compact**: project, version and date only. |
| **Changelog style** | **Plain text** (default), **Code block** or **No changelog** |
| **Changelog length** | 100 to 4000 characters, 2000 by default. Longer changelogs are cut and end with a link to Modrinth. |

Click **Preview** to see an example message with the current settings. Only you can see it.

![Update message in Compact style](/update-compact.png)
