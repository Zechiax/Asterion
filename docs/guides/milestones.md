# Download milestones

Asterion can post a message when a project reaches a download milestone, for example 100,000 downloads.

Milestones are off by default. To turn them on, open `/manage` → Notifications and click **Enable** under
Download milestones. Server settings need Administrator.

## Milestones

The server uses one of these sets:

| Set | Milestones |
| --- | --- |
| Standard (default) | 1k, 2.5k, 5k, 10k, 25k, 50k, 100k, 250k, 500k, 1M, ... |
| Round | 1k, 10k, 100k, 1M, ... |
| Major | 100k, 1M, 10M, ... |

Downloads are checked every 10 minutes. If a project passes more than one milestone between two checks, only the
highest one is posted.

Milestones that a project reached before Asterion started tracking it are not posted. If Modrinth lowers a project's
download count, for example after removing bot downloads, the same milestone is not posted again when the count goes
back up.

## Which projects

- Subscribed projects: every subscription posts milestones. To turn them off for one project, open the project in
  `/manage` and click **Milestones: on**.
- Followed creators: each of the creator's projects posts milestones if the follow has the
  **Project download milestones** event. Milestones are counted per project, not from the creator's total downloads.
  Ignored projects don't post milestones.

If a project is subscribed and also covered by a follow, the subscription decides. The follow is not used for that
project, even if the subscription has milestones turned off. If several follows cover the same project, the oldest
follow is used.

The server gets at most one message for each milestone.

## Channel

The message goes to the first channel that is set:

1. The milestone channel of the subscription or follow
2. The server's milestone channel
3. For a subscription, its update channel. For a follow, its new projects channel, or its update channel if that is
   not set.

The milestone channel of a subscription or follow only shows up in its settings while milestones are on for the
server.

## Ping role

Milestone messages ping the server's milestone ping role. If it is not set, nobody is pinged. The ping roles of
subscriptions and follows are not used for milestones.
