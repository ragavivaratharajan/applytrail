using System.ComponentModel.DataAnnotations;
using ApplyTrail.Application.Logic;
using ApplyTrail.Domain;

namespace ApplyTrail.Application.Dtos;

public abstract class ApplicationFields : IValidatableObject
{
    [Required, StringLength(150)]
    public string Company { get; set; } = "";

    [Required, StringLength(150)]
    public string RoleTitle { get; set; } = "";

    [StringLength(2000)]
    public string? JobUrl { get; set; }

    public JobSource Source { get; set; } = JobSource.Other;

    [StringLength(150)]
    public string? Location { get; set; }

    public WorkMode WorkMode { get; set; } = WorkMode.Hybrid;

    [Range(0, 10_000_000)]
    public int? SalaryMin { get; set; }

    [Range(0, 10_000_000)]
    public int? SalaryMax { get; set; }

    [Required, RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be a 3-letter code such as EUR.")]
    public string Currency { get; set; } = "EUR";

    public Sponsorship Sponsorship { get; set; } = Sponsorship.Unknown;

    public Priority Priority { get; set; } = Priority.Medium;

    [StringLength(5000)]
    public string? Notes { get; set; }

    /// <summary>The user's calendar date they applied. Defaults to today for non-Saved applications.</summary>
    public DateOnly? AppliedOn { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(JobUrl)
            && !(Uri.TryCreate(JobUrl.Trim(), UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            yield return new ValidationResult("Job URL must start with http:// or https://.", [nameof(JobUrl)]);
        }

        if (SalaryMin is { } min && SalaryMax is { } max && max < min)
        {
            yield return new ValidationResult("Maximum salary must be at least the minimum.", [nameof(SalaryMax)]);
        }
    }
}

public class CreateApplicationRequest : ApplicationFields
{
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;

    /// <summary>The user's local date. Optional; the server falls back to the UTC date.</summary>
    public DateOnly? Today { get; set; }
}

public class UpdateApplicationRequest : ApplicationFields;

public class ChangeStatusRequest
{
    public ApplicationStatus Status { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public DateOnly? Today { get; set; }
}

public class CreateEventRequest
{
    public ApplicationActivity Type { get; set; } = ApplicationActivity.Note;

    [Required, StringLength(150)]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string? Details { get; set; }

    public DateTime? OccurredAt { get; set; }
}

/// <summary>The event types a user may log by hand (status changes are recorded automatically).</summary>
public enum ApplicationActivity { Note, Email, PhoneScreen, Interview, Assessment, Offer }

public class ApplicationQuery
{
    public string? Q { get; set; }
    public ApplicationStatus[]? Status { get; set; }
    public JobSource? Source { get; set; }
    public Priority? Priority { get; set; }
    public Sponsorship? Sponsorship { get; set; }
    public bool Archived { get; set; }

    /// <summary>company | appliedOn | priority | created | lastActivity (default)</summary>
    public string Sort { get; set; } = "lastActivity";

    /// <summary>asc | desc (default)</summary>
    public string Dir { get; set; } = "desc";

    [Range(1, 10_000)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 50;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record ApplicationDto(
    Guid Id,
    string Company,
    string RoleTitle,
    string? JobUrl,
    JobSource Source,
    string? Location,
    WorkMode WorkMode,
    int? SalaryMin,
    int? SalaryMax,
    string Currency,
    Sponsorship Sponsorship,
    Priority Priority,
    ApplicationStatus Status,
    DateOnly? AppliedOn,
    DateTime LastActivityAt,
    DateTime CreatedAt,
    bool IsArchived,
    bool IsStale,
    DateOnly? NextReminderOn,
    string? Notes);

public record ActivityEventDto(
    Guid Id,
    ActivityType Type,
    DateTime OccurredAt,
    string Title,
    string? Details,
    ApplicationStatus? FromStatus,
    ApplicationStatus? ToStatus);

public record ApplicationDetailDto(
    ApplicationDto Application,
    IReadOnlyList<ActivityEventDto> Events,
    IReadOnlyList<ReminderDto> Reminders);

public class DuplicateCheckRequest
{
    [Required, StringLength(150)]
    public string Company { get; set; } = "";

    [StringLength(150)]
    public string? RoleTitle { get; set; }

    [StringLength(2000)]
    public string? JobUrl { get; set; }

    /// <summary>When editing, the application being edited (so it does not match itself).</summary>
    public Guid? ExcludeId { get; set; }
}

public record DuplicateMatchDto(
    Guid Id,
    string Company,
    string RoleTitle,
    ApplicationStatus Status,
    DateOnly? AppliedOn,
    bool IsArchived,
    DuplicateReason Reason);

/// <summary>Everything the user owns, for backup or moving to another tool.</summary>
public record FullExportDto(DateTime ExportedAt, int Version, IReadOnlyList<ApplicationDetailDto> Applications);
