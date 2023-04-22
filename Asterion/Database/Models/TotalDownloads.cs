using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("TotalDownloads")]
public class TotalDownloads
{
    [Key] public int Id { get; set; }
    [Required] public string ProjectId { get; set; }
    [Required] public int Downloads { get; set; }
    [Required] public DateTime Timestamp { get; set; }

    // Navigation property for the project associated with these total downloads
    [ForeignKey("ProjectId")]
    public ModrinthProject Project { get; set; } = null!;
}