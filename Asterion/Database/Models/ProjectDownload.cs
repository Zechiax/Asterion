using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("ProjectDownloads")]
public class ProjectDownload
{
    [Key] public int Id { get; set; }
    [Required] public string VersionId { get; set; } = null!;
    [Required] public int Downloads { get; set; }
    [Required] public DateTime Date { get; set; }

    // Foreign key to the Modrinth project this download is associated with
    [ForeignKey("ModrinthProject")]
    public string ProjectId { get; set; } = null!;
    public ModrinthProject Project { get; set; } = null!;
}