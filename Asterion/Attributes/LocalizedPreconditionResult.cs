using Asterion.Common;
using Discord.Interactions;

namespace Asterion.Attributes;

/// <summary>
///     Failed precondition with the card shown to the user, the error reason is the card's key for the logs
/// </summary>
public class LocalizedPreconditionResult : PreconditionResult
{
    private LocalizedPreconditionResult(StatusCard card) : base(InteractionCommandError.UnmetPrecondition, card.Key)
    {
        Card = card;
    }

    public StatusCard Card { get; }

    public static LocalizedPreconditionResult FromError(StatusCard card)
    {
        return new LocalizedPreconditionResult(card);
    }
}
