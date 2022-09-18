using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RinthBot.Database.Models
{
    [Table("Guilds")]
    public class Guild
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Required]
        public ulong GuildId { get; set; }
        public ulong? UpdateChannel { get; set; }
        public MessageStyle MessageStyle { get; set; } = MessageStyle.Full;

        /// <summary>
        /// If all settings should be removed after the bot left the guild
        /// </summary>
        public bool? RemoveOnLeave { get; set; } = true;

        /// <summary>
        /// If the guild is active
        /// </summary>
        public bool? Active { get; set; } = true;

        /// <summary>
        /// Whether the channel selection should be hidden after subscribe
        /// </summary>
        public bool? HideChannelSelection { get; set; } = false;
        public ulong? PingRole { get; set; }
        public ulong? ManageRole { get; set; }
        
        [ForeignKey("ModrinthArray")]
        public ulong ModrinthArrayId { get; set; }
        public virtual Array ModrinthArray { get; set; } = null!;
        
        [Required]
        public DateTime Created { get; set; }
    }
}
