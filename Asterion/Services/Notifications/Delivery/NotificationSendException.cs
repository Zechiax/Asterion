namespace Asterion.Services.Notifications.Delivery;

/// <summary>Wraps any failure to actually deliver a message, with the target context attached.</summary>
public class NotificationSendException : Exception
{
    public NotificationSendException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
