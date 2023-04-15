using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("ModrinthEntries")]
public class ModrinthEntry
{
    [Key] public ulong EntryId { get; set; }

    [Required] public ulong ArrayId { get; set; }

    public virtual Array Array { get; set; } = null!;

    public ulong? CustomUpdateChannel { get; set; }

    [Required] public string ProjectId { get; set; } = null!;

    public virtual ModrinthProject Project { get; set; } = null!;

    [Required] public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    [Required] public DateTime Created { get; set; }
}