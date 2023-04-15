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

    public long ChangeLogMaxLength { get; set; } = 2000;
}