using System;

namespace CatAdaptive.App.Models;

public record Notification(
    string Message,
    NotificationType Type = NotificationType.Info,
    TimeSpan Duration = default)
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public bool IsVisible { get; set; } = true;
}

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}
