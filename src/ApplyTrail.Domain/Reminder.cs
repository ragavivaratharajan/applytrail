namespace ApplyTrail.Domain;

/// <summary>
/// Something the user still has to do. Due dates are calendar dates in the user's own day,
/// not instants, so the server never needs to know the user's time zone.
/// </summary>
public class Reminder
{
    private Reminder() { } // EF Core

    public Guid Id { get; private set; }
    public Guid JobApplicationId { get; private set; }
    public ReminderType Type { get; private set; }
    public string Title { get; private set; } = "";
    public DateOnly DueOn { get; private set; }

    /// <summary>Local time of day; used for interviews.</summary>
    public TimeOnly? TimeOfDay { get; private set; }

    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public bool IsOpen => CompletedAt is null && CancelledAt is null;

    internal static Reminder Create(
        Guid applicationId, ReminderType type, string title, DateOnly dueOn, TimeOnly? timeOfDay) => new()
    {
        Id = Guid.NewGuid(),
        JobApplicationId = applicationId,
        Type = type,
        Title = title,
        DueOn = dueOn,
        TimeOfDay = timeOfDay,
    };

    internal void Complete(DateTime nowUtc) => CompletedAt ??= nowUtc;

    internal void Cancel(DateTime nowUtc)
    {
        if (IsOpen) CancelledAt = nowUtc;
    }

    internal void MoveTo(DateOnly dueOn) => DueOn = dueOn;
}
