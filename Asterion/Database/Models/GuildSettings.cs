using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("GuildSettings")]
public class GuildSettings
{
    [Key] public ulong GuildSettingsId { get; set; }

    [ForeignKey("Guild")] public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    /// <summary>
    ///     Whether the channel selection should be hidden after subscribe
    /// </summary>
    public bool? ShowChannelSelection { get; set; } = true;

    /// <summary>
    ///     If all settings should be removed after the bot left the guild
    /// </summary>
    public bool? RemoveOnLeave { get; set; } = true;

    /// <summary>
    ///     Show subscribe button in embeds
    /// </summary>
    public bool? ShowSubscribeButton { get; set; } = true;

    public bool? CheckMessagesForModrinthLink { get; set; } = false;
    public MessageStyle MessageStyle { get; set; } = MessageStyle.Full;

    public ChangelogStyle ChangelogStyle { get; set; } = ChangelogStyle.PlainText;

    public const int DefaultChangeLogMaxLength = 2000;

    public long ChangeLogMaxLength { get; set; } = DefaultChangeLogMaxLength;

    /// <summary>
    ///     Discord locale code chosen by a server admin (e.g. "cs", "de"). Null = follow Discord's own locale.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    ///     If messages are sent when a project reaches a download milestone
    /// </summary>
    public bool MilestonesEnabled { get; set; }

    public MilestoneLadder MilestoneLadder { get; set; } = MilestoneLadder.Standard;

    /// <summary>
    ///     Channel for all milestone messages, null = the channel of each subscription or follow
    /// </summary>
    public ulong? MilestoneChannel { get; set; }

    /// <summary>
    ///     Role pinged in milestone messages, null = nobody is pinged
    /// </summary>
    public ulong? MilestonePingRole { get; set; }
}