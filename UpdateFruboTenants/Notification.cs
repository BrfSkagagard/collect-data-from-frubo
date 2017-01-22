using System;

namespace UpdateFruboTenants
{
    public class Notification
    {
        public int ApartmentNumber { get; set; }
        public NotificationType Type { get; set; }
        public string Message { get; set; }
        public string ReadMoreLink { get; set; }
    }

    [Flags]
    public enum NotificationType
    {
        Normal,
        Warning,
        Critical
    }

    [Flags]
    public enum NotificationSource
    {
        Frubo
    }
}
