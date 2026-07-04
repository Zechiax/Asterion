namespace Asterion.Services.Notifications.Delivery;

public class NotificationSenderRouter
{
    private readonly IEnumerable<INotificationSender> _senders;

    public NotificationSenderRouter(IEnumerable<INotificationSender> senders)
    {
        _senders = senders;
    }

    public Task SendAsync(DeliveryTarget target, NotificationContent content, CancellationToken ct)
    {
        var sender = _senders.FirstOrDefault(s => s.CanHandle(target));

        if (sender is null)
            throw new InvalidOperationException(
                $"No registered {nameof(INotificationSender)} can handle target of type {target.GetType().Name}");

        return sender.SendAsync(target, content, ct);
    }
}
