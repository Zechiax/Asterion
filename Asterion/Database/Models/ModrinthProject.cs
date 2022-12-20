using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models
{
    [Table("ModrinthProjects")]
    public class ModrinthProject
    {
        [Key]
        public string ProjectId { get; set; } = null!;
        public string? Title { get; set; }
        [Required] public string LastCheckVersion { get; set; } = null!;
        public DateTime? LastUpdated { get; set; }

        [Required] public DateTime Created { get; set; }
    }
}
