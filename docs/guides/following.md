# Following creators

Follow a Modrinth user to get updates for all of their projects, and a message when they publish a new one.

You need the Administrator permission or the server's manage role to add or change follows.

## Follow a creator

Run `/follow user`:

```
/follow user username:jellysquid3 channel:#mod-updates
```

| Option | What it does |
| --- | --- |
| `username` | Username, ID or profile link of the Modrinth user |
| `channel` | Where the messages go. The current channel if not set. |
| `events` | Which messages to send. See below. |
| `new_project_channel` | Where new project messages go. Same as `channel` if not set. |

`events` has three choices:

- **Project updates and new projects** (default)
- **Only project updates**
- **Only new projects**

You can also follow from:

- the **Follow user** button under `/user`, or under **See owner** on a project. It uses the current channel and
  turns on both events.
- **Follow creator** in `/manage`.

## New projects

A new project message is sent when the creator publishes a project after you followed them. Projects that existed
before are added without a message.

## Change a follow

1. Run `/manage`.
2. Open the **Creators** tab.
3. Click **Settings** next to the creator.

![Settings of a follow](/follow-settings.png)

| Setting | What it does |
| --- | --- |
| **Channel** | Where update messages go |
| **New projects channel override** | Where new project messages go. Leave it empty to use **Channel**. |
| **Ping role override** | Role to ping instead of the server's default ping role |
| **New projects ping role override** | Role to ping for new projects. Leave it empty to use the ping role above. |
| **Events** | **Project updates** and **New projects**. With nothing selected, no messages are sent. |
| **Release types** | Which versions are posted: Alpha, Beta, Release |
| **Loaders** | Only post versions for these loaders. Leave it empty to post all of them. |

## Ignore a project

To stop update messages for one of the creator's projects:

1. Open the follow's **Settings**.
2. Click **Projects** (the button shows how many are ignored).
3. Click **Ignore** next to the project.

Click **Unignore** to get its updates again. If the list is empty right after following, try again in a few minutes.

## Unfollow

Open the follow's **Settings**, click **Unfollow**, then **Confirm**.

To remove all follows at once, use **Unfollow all creators** in the [General](/guides/manage#general) tab.
