namespace ApplyTrail.Domain;

/// <summary>One entry on an application's timeline: something that already happened.</summary>
public class ActivityEvent
{
    private ActivityEvent() { } // EF Core

    public Guid Id { get; private set; }
    public Guid JobApplicationId { get; private set; }
    public ActivityType Type { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string Title { get; private set; } = "";
    public string? Details { get; private set; }

    /// <summary>Only set on <see cref="ActivityType.StatusChange"/> events.</summary>
    public ApplicationStatus? FromStatus { get; private set; }
    public ApplicationStatus? ToStatus { get; private set; }

    internal static ActivityEvent Create(
        Guid applicationId, ActivityType type, string title, string? details, DateTime occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        JobApplicationId = applicationId,
        Type = type,
        Title = title,
        Details = details,
        OccurredAt = occurredAt,
    };

    internal static ActivityEvent StatusChange(
        Guid applicationId, ApplicationStatus? from, ApplicationStatus to, DateTime occurredAt, string? note) => new()
    {
        Id = Guid.NewGuid(),
        JobApplicationId = applicationId,
        Type = ActivityType.StatusChange,
        Title = from is null ? $"Added as {to}" : $"Moved to {to}",
        Details = note,
        OccurredAt = occurredAt,
        FromStatus = from,
        ToStatus = to,
    };
}
