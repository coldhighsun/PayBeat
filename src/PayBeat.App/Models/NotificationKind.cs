namespace PayBeat.App.Models;

/// <summary>
/// Category of an in-app notification; decides the accent color of its toast.
/// </summary>
public enum NotificationKind
{
    /// <summary>
    /// Positive event such as a reached earnings milestone.
    /// </summary>
    Success,

    /// <summary>
    /// Time-sensitive reminder such as the end-of-day warning.
    /// </summary>
    Reminder,

    /// <summary>
    /// Neutral information such as an available update.
    /// </summary>
    Info,
}
