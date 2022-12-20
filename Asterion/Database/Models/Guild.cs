using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models
{
    [Table("Guilds")]
    public class Guild
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Required]
        public ulong GuildId { get; set; }
        
        //BUG: Forgot to add this as ForeignKey, in database when adding guild it's value is 0, needs to be added carefully
        public ulong GuildSettingsId { get; set; }
        public virtual GuildSettings GuildSettings { get; set; } = null!;

        /// <summary>
        /// If the guild is active
        /// </summary>
        public bool? Active { get; set; } = true;
        public ulong? PingRole { get; set; }
        public ulong? ManageRole { get; set; }
        
        [ForeignKey("ModrinthArray")]
        public ulong ModrinthArrayId { get; set; }
        public virtual Array ModrinthArray { get; set; } = null!;
        
        [Required]
        public DateTime Created { get; set; }
    }
}
