# Charts

Chart the downloads or new followers of a project.

```
/chart downloads project:sodium
/chart followers project:sodium range:7 days
```

| Command | Shows |
| --- | --- |
| `/chart downloads` | Downloads per period |
| `/chart followers` | New followers per period, not the total |

![Downloads chart of Sodium over 30 days](/chart.png)

## Options

- `project`: slug or ID. Administrators and members with the manage role get suggestions from this server's
  subscriptions. You can type any project.
- `range`: 24 hours, 7 days, 30 days (default), 90 days or 1 year.

## Where the data comes from

Asterion collects the data itself, for every project that a server subscribes to or follows. A project nobody tracks
has no chart. Once someone subscribes to it or follows its creator, the chart fills up over time.

If there isn't enough data yet, the bot tells you when to try again.
