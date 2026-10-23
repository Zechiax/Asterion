namespace Asterion.Database.Models;

/// <summary>
///     Last download and follower counts of a project seen in a day, kept forever
/// </summary>
public class DailyProjectStats
{
    public string ProjectId { get; set; } = null!;

    /// <summary>UTC midnight of the day</summary>
    public DateTime Day { get; set; }

    public int Downloads { get; set; }
    public int Followers { get; set; }
}
