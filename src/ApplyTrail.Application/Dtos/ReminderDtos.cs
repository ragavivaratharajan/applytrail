using System.ComponentModel.DataAnnotations;
using ApplyTrail.Domain;

namespace ApplyTrail.Application.Dtos;

public enum ReminderBucket { All, Overdue, Today, Upcoming }

public class CreateReminderRequest
{
    public ReminderType Type { get; set; } = ReminderType.FollowUp;

    [Required, StringLength(150)]
    public string Title { get; set; } = "";

    public DateOnly DueOn { get; set; }

    public TimeOnly? TimeOfDay { get; set; }
}

public class CompleteReminderRequest
{
    /// <summary>Record the reminder on the application's timeline.</summary>
    public bool LogAsEvent { get; set; } = true;

    [StringLength(2000)]
    public string? Note { get; set; }

    /// <summary>Schedule the same reminder again this many days from today.</summary>
    [Range(1, 90)]
    public int? RemindAgainInDays { get; set; }

    public DateOnly? Today { get; set; }
}

public class SnoozeReminderRequest
{
    [Range(1, 90)]
    public int Days { get; set; } = 3;

    public DateOnly? Today { get; set; }
}

public record ReminderDto(
    Guid Id,
    Guid ApplicationId,
    string Company,
    string RoleTitle,
    ReminderType Type,
    string Title,
    DateOnly DueOn,
    TimeOnly? TimeOfDay,
    DateTime? CompletedAt,
    DateTime? CancelledAt);
