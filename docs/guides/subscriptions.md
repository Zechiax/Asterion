# Project subscriptions

A subscription posts a message in a channel every time the project publishes a new version.

You need the Administrator permission or the server's manage role to add or change subscriptions.

## Subscribe to a project

There are three ways. All of them accept a slug, an ID or a Modrinth link.

- **`/subscribe`**: `/subscribe project-id:sodium custom-channel:#mod-updates`. Without `custom-channel`, updates go
  to the channel you ran the command in.
- **Subscribe button**: under `/search` and `/random` results. Updates go to the channel the button is in.
- **Add project** in `/manage`: enter the project and pick a channel. The project's settings open right after.

The bot needs **View Channels**, **Send Messages** and **Embed Links** in the update channel.

Only versions published after you subscribed are posted.

## Change a subscription

1. Run `/manage`.
2. Open the **Projects** tab.
3. Click **Settings** next to the project.

![Settings of a subscription](/subscription-settings.png)

| Setting | What it does |
| --- | --- |
| **Channel** | Where the update messages go |
| **Ping role override** | Role to ping instead of the server's default ping role. Leave it empty to use the default. |
| **Release types** | Which versions are posted: Alpha, Beta, Release. All three are on by default. Keep at least one. |
| **Loaders** | Only post versions for these loaders. Leave it empty to post all of them. Shown only if the project lists loaders. |

Only the person who opened `/manage` can use these controls.

## Update messages

Each new version gets its own message with a **View on Modrinth** button. The color of the message shows the release
type: green for Release, yellow for Beta, red for Alpha.

![Update message in Full style](/update-full.png)

To make the messages shorter or change how the changelog looks, see
[Notifications](/guides/manage#notifications) in the `/manage` guide.

## Unsubscribe

- **`/unsubscribe`**: `/unsubscribe project:sodium`, then **Confirm**. The option suggests the server's
  subscriptions and also accepts a slug, an ID or a Modrinth link.
- In `/manage`: open the project's **Settings**, click **Unsubscribe**, then **Confirm**.
- Under `/search`: the button shows **Unsubscribe** for projects the server is subscribed to.

To remove all subscriptions at once, use **Unsubscribe from all projects** in the
[General](/guides/manage#general) tab.

## Subscribed and following the creator

If you subscribe to a project and also follow its creator, you only get one message per version. The subscription's
settings are used, and the follow is skipped for that project.
