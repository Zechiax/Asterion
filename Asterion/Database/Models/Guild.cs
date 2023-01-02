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
        
        [ForeignKey("GuildSettings")]
        public ulong GuildSettingsId { get; set; }
        public virtual GuildSettings Settings { get; set; } = null!;
        public ulong? PingRole { get; set; }
        public ulong? ManageRole { get; set; }
        
        public virtual ICollection<GuildModrinthEntry> GuildModrinthEntries { get; set; } = new List<GuildModrinthEntry>();
        
        [Required]
        public DateTime Created { get; set; }
    }
}
