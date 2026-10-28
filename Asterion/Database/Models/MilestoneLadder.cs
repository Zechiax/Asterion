namespace Asterion.Database.Models;

/// <summary>
///     Which download milestones a guild gets messages for
/// </summary>
public enum MilestoneLadder
{
    /// <summary>1, 2.5 and 5 of every power of ten from 1,000</summary>
    Standard = 0,

    /// <summary>Every power of ten from 1,000</summary>
    Round = 1,

    /// <summary>Every power of ten from 100,000</summary>
    Major = 2
}
