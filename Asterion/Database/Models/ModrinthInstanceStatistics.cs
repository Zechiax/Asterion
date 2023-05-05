using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

[Table("ModrinthInstanceStatistics")]
public class ModrinthInstanceStatistics
{
    public int Id { get; set; }
    public int Projects { get; set; }
    public int Versions { get; set; }
    public int Files { get; set; }
    public int Authors { get; set; }
}