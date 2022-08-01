using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RinthBot.Database
{
    [Table("Guilds")]
    public class Guild
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Required]
        public ulong GuildId { get; set; }
        public ulong? UpdateChannel { get; set; }
        public ulong MessageStyle { get; set; }
        public bool? RemoveOnLeave { get; set; } = true;
        public bool? Active { get; set; }
        public ulong? PingRole { get; set; }
        public ulong? ManageRole { get; set; }
        
        [ForeignKey("ModrinthArray")]
        public ulong ModrinthArrayId { get; set; }
        public virtual Array ModrinthArray { get; set; } = null!;
        
        [Required]
        public DateTime Created { get; set; }
    }
}
