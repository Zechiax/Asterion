namespace Asterion.Services.Notifications.Delivery;

public interface INotificationSender
{
    bool CanHandle(DeliveryTarget target);
    Task SendAsync(DeliveryTarget target, NotificationContent content, CancellationToken ct);
}
