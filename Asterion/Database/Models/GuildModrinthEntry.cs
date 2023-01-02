using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("GuildModrinthEntries")]
public class GuildModrinthEntry
{
    [Key]
    public ulong EntryId { get; set; }

    public ProjectType ProjectType { get; set; }
    public ulong? CustomUpdateChannel { get; set; }

    [Required]
    public string ProjectId { get; set; } = null!;
    public virtual ModrinthProject Project { get; set; } = null!;

    [Required]
    public ulong GuildId { get; set; }
    public virtual Guild Guild { get; set; } = null!;

    [Required]
    public DateTime Created { get; set; }
}