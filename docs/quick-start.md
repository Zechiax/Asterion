# Quick start

From invite to your first update message in a few minutes.

## Invite the bot

1. Open the [invite link](https://discord.com/api/oauth2/authorize?client_id=986993814671614094&permissions=537054208&scope=bot%20applications.commands) and pick your server.
2. Keep the requested permissions. In the channel where updates should go, the bot needs **View Channels**, **Send Messages** and **Embed Links**.

Asterion only works in servers, not in DMs.

## Find a project

Run `/search` with a name, slug or ID:

```
/search query:sodium
```

The bot shows the first match. The buttons under it:

| Button | What it does |
| --- | --- |
| **Subscribe** | Sends updates of this project to the current channel |
| **Project's site** | Opens the project on Modrinth |
| **See owner** | Shows the project's owner, with a button to follow them |
| **View more results** | Lists other matches |

![Result of /search with its buttons](/search.png)

You can also right-click any message, choose **Apps**, then **Find project on Modrinth**. It searches for the message's text.

## Subscribe to updates

You need the Administrator permission or the server's manage role for this.

1. Run `/subscribe` with the project and the channel for the updates:
   ```
   /subscribe project-id:sodium custom-channel:#mod-updates
   ```
   Leave out `custom-channel` to use the current channel.
2. Wait for the next version. The bot posts one message per new version.

Versions released before you subscribed are not posted.

The **Subscribe** button under `/search` does the same, using the channel the button is in.

![Update message in Full style](/update-full.png)

## Follow a creator

Following a Modrinth user gets you updates for all of their projects, and a message when they publish a new one.

```
/follow user username:jellysquid3 channel:#mod-updates
```

The **Follow user** button under `/user` and under **See owner** does the same in the current channel.

See [Following creators](/guides/following) for the options.

## Set up roles

Run `/manage` and open the **General** tab. Only Administrators can change these.

- **Manage role**: members with this role can subscribe, follow and use `/manage` without being Administrators.
- **Default ping role**: this role gets pinged on every update message. Leave it empty to ping nobody.

## Next steps

- [Project subscriptions](/guides/subscriptions): filter by release type or loader, change the channel
- [Following creators](/guides/following): ignore single projects, pick which events to get
- [The /manage panel](/guides/manage): change how update messages look
- [Commands](/commands): every command on one page
