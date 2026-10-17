using Discord.Interactions;
using Discord.WebSocket;

namespace Asterion.Common;

/// <summary>
///     Parameter of modal handlers that read the submitted values themselves with <see cref="ModalValues" />,
///     as the modals are built at runtime (e.g. with the current channel preselected)
/// </summary>
public class RawModal : IModal
{
    public string Title => string.Empty;
}

public static class ModalValues
{
    /// <summary>
    ///     Text of a text input, null if it is not in the submitted modal
    /// </summary>
    public static string? GetText(this SocketModal modal, string customId)
    {
        return Find(modal, customId)?.Value;
    }

    /// <summary>
    ///     Selected values of a select menu, empty if it is not in the submitted modal
    /// </summary>
    public static string[] GetSelected(this SocketModal modal, string customId)
    {
        return Find(modal, customId)?.Values?.ToArray() ?? [];
    }

    private static SocketMessageComponentData? Find(SocketModal modal, string customId)
    {
        return modal.Data.Components.FirstOrDefault(c => c.CustomId == customId);
    }
}
