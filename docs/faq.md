# FAQ

## No update messages arrive

Check these in order:

1. The bot has **View Channels**, **Send Messages** and **Embed Links** in the update channel.
2. The version matches the **Release types** and **Loaders** of the subscription or follow.
3. For a follow: the project is not ignored, and **Events** includes **Project updates**.
4. The version was published after you subscribed. Older versions are never posted.

The bot checks Modrinth regularly, not instantly, so a new version can take a few minutes to show up.

## A role isn't pinged

Check that a ping role is set: **Ping role override** in the subscription or follow, or **Default ping role** in
[General](/guides/manage#general). If the role still isn't pinged, make it mentionable in the server's role settings, or give the bot the
**Mention Everyone** permission.

## "For this, you either need the Administrator permission or the manage role of this server"

You need to be an Administrator, or an Administrator has to set a **Manage role** in `/manage` → **General** and give
it to you.

## "Only the user who executed the command can use this"

Someone else opened that `/manage` panel. Run `/manage` yourself.

## The General and Notifications tabs are greyed out

Only Administrators can change server settings.

## Commands don't work in DMs

Asterion only works in servers.

## The bot no longer embeds Modrinth links

Automatic link embeds were removed. Right-click the message, choose **Apps**, then **Find project on Modrinth**.

## A chart says there are no statistics

The bot only records data for projects that a server subscribes to or follows. See [Charts](/guides/charts).

## The bot doesn't respond

If the bot is online but doesn't respond, check [Modrinth's status page](https://status.modrinth.com/). Modrinth can
be offline.

## Bug reports and feature suggestions

Open a [new issue](https://github.com/Zechiax/Asterion/issues/new) on GitHub.
