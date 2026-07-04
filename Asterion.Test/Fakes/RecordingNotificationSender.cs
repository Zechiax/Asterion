using Asterion.Services.Notifications.Delivery;

namespace Asterion.Test.Fakes;

/// <summary>
///     A handwritten fake rather than a mock - clearer for asserting a multistep async workflow where we
///     want to inspect exactly what was "really sent" after the fact.
/// </summary>
internal class RecordingNotificationSender : INotificationSender
{
    public List<(DeliveryTarget Target, NotificationContent Content)> Sent { get; } = new();
    public Exception? ThrowOnSend { get; set; }

    public bool CanHandle(DeliveryTarget target) => true;

    public Task SendAsync(DeliveryTarget target, NotificationContent content, CancellationToken ct)
    {
        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        Sent.Add((target, content));
        return Task.CompletedTask;
    }
}
