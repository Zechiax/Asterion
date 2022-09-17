using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RinthBot.Database.Models;

[Table("Arrays")]
public class Array
{
    [Key]
    public ulong ArrayId { get; set; }

    public ArrayType Type { get; set; }
    
    [ForeignKey("GuildId")]
    public ulong GuildId { get; set; }
    public virtual Guild Guild { get; set; } = null!;
}