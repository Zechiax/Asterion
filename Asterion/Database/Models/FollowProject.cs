using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Asterion.Database.Models;

/// <summary>
///     A project known to a follow. Doubles as the follow's ignore list (<see cref="Ignored" />).
/// </summary>
[Table("FollowProjects")]
public class FollowProject
{
    public int FollowId { get; set; }
    public virtual ModrinthFollow Follow { get; set; } = null!;

    [Required] public string ProjectId { get; set; } = null!;
    public virtual ModrinthProject Project { get; set; } = null!;

    /// <summary>If true, no update messages are sent for this project.</summary>
    public bool Ignored { get; set; }

    [Required] public DateTime FirstSeen { get; set; }
}
