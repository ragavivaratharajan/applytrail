using ApplyTrail.Application.Abstractions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using ApplyTrail.Domain;
using Microsoft.EntityFrameworkCore;
using static ApplyTrail.Application.Services.ServiceHelpers;

namespace ApplyTrail.Application.Services;

public sealed class ReminderService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Open reminders across all live (not archived) applications, soonest first.</summary>
    public async Task<IReadOnlyList<ReminderDto>> ListAsync(ReminderBucket bucket, DateOnly? todayFromClient, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var today = ResolveToday(todayFromClient, Now);
        var horizon = today.AddDays(DashboardCalculator.UpcomingWindowDays);

        var applications = await db.Applications.AsNoTracking()
            .Where(a => a.UserId == userId
                        && a.ArchivedAt == null
                        && a.Reminders.Any(r => r.CompletedAt == null && r.CancelledAt == null))
            .Include(a => a.Reminders)
            .ToListAsync(ct);

        bool InBucket(DateOnly due) => bucket switch
        {
            ReminderBucket.Overdue => due < today,
            ReminderBucket.Today => due == today,
            ReminderBucket.Upcoming => due > today && due <= horizon,
            _ => true,
        };

        return applications
            .SelectMany(a => a.Reminders.Where(r => r.IsOpen).Select(r => (Application: a, Reminder: r)))
            .Where(x => InBucket(x.Reminder.DueOn))
            .OrderBy(x => x.Reminder.DueOn)
            .ThenBy(x => x.Reminder.TimeOfDay ?? TimeOnly.MaxValue)
            .ThenBy(x => x.Application.Company, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Reminder.ToDto(x.Application))
            .ToList();
    }

    public async Task<ReminderDto> CreateAsync(Guid applicationId, CreateReminderRequest request, CancellationToken ct)
    {
        if (request.DueOn == default)
        {
            throw new BadRequestException("A due date is required.");
        }

        var application = await LoadAsync(applicationId, ct);
        var reminder = application.AddReminder(request.Type, request.Title.Trim(), request.DueOn, request.TimeOfDay);

        await db.SaveChangesAsync(ct);
        return reminder.ToDto(application);
    }

    public async Task<ReminderDto> CompleteAsync(
        Guid applicationId, Guid reminderId, CompleteReminderRequest request, CancellationToken ct)
    {
        var now = Now;
        var today = ResolveToday(request.Today, now);
        var application = await LoadAsync(applicationId, ct);

        Reminder reminder;
        try
        {
            reminder = application.CompleteReminder(
                reminderId, now, today, request.LogAsEvent, Clean(request.Note), request.RemindAgainInDays);
        }
        catch (InvalidOperationException)
        {
            throw new NotFoundException("Reminder not found.");
        }

        await db.SaveChangesAsync(ct);
        return reminder.ToDto(application);
    }

    public async Task<ReminderDto> SnoozeAsync(
        Guid applicationId, Guid reminderId, SnoozeReminderRequest request, CancellationToken ct)
    {
        var today = ResolveToday(request.Today, Now);
        var application = await LoadAsync(applicationId, ct);

        var existing = application.FindReminder(reminderId) ?? throw new NotFoundException("Reminder not found.");
        if (!existing.IsOpen)
        {
            throw new ConflictException("Only open reminders can be snoozed.");
        }

        var reminder = application.SnoozeReminder(reminderId, request.Days, today);

        await db.SaveChangesAsync(ct);
        return reminder.ToDto(application);
    }

    private async Task<JobApplication> LoadAsync(Guid applicationId, CancellationToken ct)
    {
        var userId = currentUser.Id;
        return await db.Applications
                   .Include(a => a.Reminders)
                   .FirstOrDefaultAsync(a => a.Id == applicationId && a.UserId == userId, ct)
               ?? throw new NotFoundException("Application not found.");
    }
}
