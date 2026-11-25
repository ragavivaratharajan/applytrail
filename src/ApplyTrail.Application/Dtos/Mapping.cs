using ApplyTrail.Domain;

namespace ApplyTrail.Application.Dtos;

/// <summary>Hand-written mapping: small, explicit and checked by the compiler.</summary>
public static class Mapping
{
    /// <summary>SQLite hands back DateTimes with an unspecified kind; everything we store is UTC.</summary>
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime? AsUtc(this DateTime? value) => value?.AsUtc();

    /// <remarks>Reminders must be loaded on <paramref name="application"/> for NextReminderOn to be accurate.</remarks>
    public static ApplicationDto ToDto(this JobApplication application, DateTime nowUtc, int staleAfterDays) => new(
        application.Id,
        application.Company,
        application.RoleTitle,
        application.JobUrl,
        application.Source,
        application.Location,
        application.WorkMode,
        application.SalaryMin,
        application.SalaryMax,
        application.Currency,
        application.Sponsorship,
        application.Priority,
        application.Status,
        application.AppliedOn,
        application.LastActivityAt.AsUtc(),
        application.CreatedAt.AsUtc(),
        application.IsArchived,
        application.IsStale(nowUtc, staleAfterDays),
        application.Reminders.Where(r => r.IsOpen).Select(r => (DateOnly?)r.DueOn).Min(),
        application.Notes);

    /// <remarks>Events and Reminders must be loaded. Newest events first; open reminders by due date, then closed ones.</remarks>
    public static ApplicationDetailDto ToDetailDto(this JobApplication application, DateTime nowUtc, int staleAfterDays) => new(
        application.ToDto(nowUtc, staleAfterDays),
        application.Events.OrderByDescending(e => e.OccurredAt).Select(e => e.ToDto()).ToList(),
        application.Reminders
            .OrderBy(r => r.IsOpen ? 0 : 1)
            .ThenBy(r => r.IsOpen ? r.DueOn : DateOnly.MinValue)
            .ThenByDescending(r => r.CompletedAt ?? r.CancelledAt)
            .Select(r => r.ToDto(application))
            .ToList());

    public static ActivityEventDto ToDto(this ActivityEvent e) => new(
        e.Id, e.Type, e.OccurredAt.AsUtc(), e.Title, e.Details, e.FromStatus, e.ToStatus);

    public static ReminderDto ToDto(this Reminder reminder, JobApplication application) => new(
        reminder.Id,
        application.Id,
        application.Company,
        application.RoleTitle,
        reminder.Type,
        reminder.Title,
        reminder.DueOn,
        reminder.TimeOfDay,
        reminder.CompletedAt.AsUtc(),
        reminder.CancelledAt.AsUtc());
}
