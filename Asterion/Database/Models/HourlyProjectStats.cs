namespace Asterion.Database.Models;

/// <summary>
///     Last download and follower counts of a project seen in an hour, kept for a rolling window
/// </summary>
public class HourlyProjectStats
{
    public string ProjectId { get; set; } = null!;

    /// <summary>UTC start of the hour</summary>
    public DateTime Hour { get; set; }

    public int Downloads { get; set; }
    public int Followers { get; set; }
}
