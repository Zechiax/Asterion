using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RinthBot.Database
{
    [Table("ModrinthProjects")]
    public class ModrinthProject
    {
        [Key]
        public string ProjectId { get; set; } = null!;
        public string? Title { get; set; }
        public string? LastCheckVersion { get; set; }
        public DateTime? LastUpdated { get; set; }

        [Required]
        public DateTime Created { get; set; }
    }
}
