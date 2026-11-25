using ApplyTrail.Application.Abstractions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using ApplyTrail.Domain;
using Microsoft.EntityFrameworkCore;
using static ApplyTrail.Application.Services.ServiceHelpers;

namespace ApplyTrail.Application.Services;

/// <summary>Everything about a user's applications. Every query is scoped to the signed-in user.</summary>
public sealed class ApplicationService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private static readonly HashSet<string> SortFields =
        new(StringComparer.OrdinalIgnoreCase) { "company", "appliedOn", "priority", "created", "lastActivity" };

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<ApplicationDto>> ListAsync(ApplicationQuery query, CancellationToken ct)
    {
        if (!SortFields.Contains(query.Sort))
        {
            throw new BadRequestException("Unknown sort field.", $"Use one of: {string.Join(", ", SortFields)}.");
        }

        var userId = currentUser.Id;
        var now = Now;
        var settings = await db.GetSettingsAsync(userId, ct);

        var filtered = db.Applications.AsNoTracking().Where(a => a.UserId == userId);

        filtered = query.Archived
            ? filtered.Where(a => a.ArchivedAt != null)
            : filtered.Where(a => a.ArchivedAt == null);

        if (query.Status is { Length: > 0 } statuses)
        {
            filtered = filtered.Where(a => statuses.Contains(a.Status));
        }

        if (query.Source is { } source)
        {
            filtered = filtered.Where(a => a.Source == source);
        }

        if (query.Priority is { } priority)
        {
            filtered = filtered.Where(a => a.Priority == priority);
        }

        if (query.Sponsorship is { } sponsorship)
        {
            filtered = filtered.Where(a => a.Sponsorship == sponsorship);
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim().ToLowerInvariant();
            filtered = filtered.Where(a =>
                a.Company.ToLower().Contains(term)
                || a.RoleTitle.ToLower().Contains(term)
                || (a.Location != null && a.Location.ToLower().Contains(term))
                || (a.Notes != null && a.Notes.ToLower().Contains(term)));
        }

        var total = await filtered.CountAsync(ct);

        var ascending = string.Equals(query.Dir, "asc", StringComparison.OrdinalIgnoreCase);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        // Include before Skip/Take: reminders are needed for NextReminderOn on each card.
        var rows = await Order(filtered, query.Sort, ascending)
            .ThenByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Include(a => a.Reminders)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(a => a.ToDto(now, settings.StaleAfterDays)).ToList();
        return new PagedResult<ApplicationDto>(items, total, page, pageSize);
    }

    public async Task<ApplicationDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var settings = await db.GetSettingsAsync(userId, ct);

        var application = await db.Applications.AsNoTracking()
            .Include(a => a.Events)
            .Include(a => a.Reminders)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct)
            ?? throw new NotFoundException("Application not found.");

        return application.ToDetailDto(Now, settings.StaleAfterDays);
    }

    public async Task<ApplicationDto> CreateAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var now = Now;
        var today = ResolveToday(request.Today, now);
        EnsureNotInFuture(request.AppliedOn, today);

        var settings = await db.GetSettingsAsync(userId, ct);

        var application = JobApplication.Create(
            userId,
            request.Company.Trim(),
            request.RoleTitle.Trim(),
            request.Status,
            request.AppliedOn,
            today,
            now,
            settings.FollowUpAfterDays);
        Apply(application, request);

        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return application.ToDto(now, settings.StaleAfterDays);
    }

    public async Task<ApplicationDto> UpdateAsync(Guid id, UpdateApplicationRequest request, CancellationToken ct)
    {
        var now = Now;
        var today = ResolveToday(null, now);
        var settings = await db.GetSettingsAsync(currentUser.Id, ct);
        var application = await LoadAsync(id, ct);

        // Validate before touching the entity, so a rejected request never leaves it half-edited.
        EnsureNotInFuture(request.AppliedOn, today);

        Apply(application, request);

        // An application that has been sent always has an applied date, so a missing value means "unchanged".
        if (request.AppliedOn is { } appliedOn && application.Status != ApplicationStatus.Saved)
        {
            application.AppliedOn = appliedOn;
        }

        await db.SaveChangesAsync(ct);
        return application.ToDto(now, settings.StaleAfterDays);
    }

    public async Task<ApplicationDto> ChangeStatusAsync(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        var now = Now;
        var today = ResolveToday(request.Today, now);
        var settings = await db.GetSettingsAsync(currentUser.Id, ct);
        var application = await LoadAsync(id, ct);

        application.ChangeStatus(request.Status, now, today, settings.FollowUpAfterDays, Clean(request.Note));

        await db.SaveChangesAsync(ct);
        return application.ToDto(now, settings.StaleAfterDays);
    }

    public async Task<ApplicationDto> ArchiveAsync(Guid id, bool archived, CancellationToken ct)
    {
        var now = Now;
        var settings = await db.GetSettingsAsync(currentUser.Id, ct);
        var application = await LoadAsync(id, ct);

        if (archived)
        {
            application.Archive(now);
        }
        else
        {
            application.Unarchive();
        }

        await db.SaveChangesAsync(ct);
        return application.ToDto(now, settings.StaleAfterDays);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var application = await LoadAsync(id, ct, includeReminders: false);
        db.Applications.Remove(application); // events and reminders go with it (cascade)
        await db.SaveChangesAsync(ct);
    }

    public async Task<ActivityEventDto> AddEventAsync(Guid id, CreateEventRequest request, CancellationToken ct)
    {
        var now = Now;
        var application = await LoadAsync(id, ct, includeReminders: false);

        var type = Enum.Parse<ActivityType>(request.Type.ToString());
        var occurredAt = (request.OccurredAt ?? now).AsUtc();
        var entry = application.AddEvent(type, request.Title.Trim(), Clean(request.Details), occurredAt, now);

        await db.SaveChangesAsync(ct);
        return entry.ToDto();
    }

    /// <summary>"Have I already applied to this?" Looks at links first, then employer plus similar title.</summary>
    public async Task<IReadOnlyList<DuplicateMatchDto>> CheckDuplicatesAsync(
        DuplicateCheckRequest request, CancellationToken ct)
    {
        var userId = currentUser.Id;

        var rows = await db.Applications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new { a.Id, a.Company, a.RoleTitle, a.JobUrl, a.Status, a.AppliedOn, a.ArchivedAt })
            .ToListAsync(ct);

        return rows
            .Where(r => r.Id != request.ExcludeId)
            .Select(r => (Row: r, Reason: DuplicateDetector.Compare(
                request.Company, request.RoleTitle ?? "", request.JobUrl, r.Company, r.RoleTitle, r.JobUrl)))
            .Where(x => x.Reason != DuplicateReason.None)
            .OrderBy(x => x.Reason) // same link is the strongest signal
            .ThenByDescending(x => x.Row.AppliedOn)
            .Take(5)
            .Select(x => new DuplicateMatchDto(
                x.Row.Id, x.Row.Company, x.Row.RoleTitle, x.Row.Status, x.Row.AppliedOn, x.Row.ArchivedAt != null, x.Reason))
            .ToList();
    }

    private async Task<JobApplication> LoadAsync(Guid id, CancellationToken ct, bool includeReminders = true)
    {
        var userId = currentUser.Id;
        IQueryable<JobApplication> query = db.Applications.Where(a => a.Id == id && a.UserId == userId);

        if (includeReminders)
        {
            query = query.Include(a => a.Reminders);
        }

        // Someone else's id and a missing id look the same on purpose.
        return await query.FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Application not found.");
    }

    private static IOrderedQueryable<JobApplication> Order(
        IQueryable<JobApplication> query, string sort, bool ascending) => sort.ToLowerInvariant() switch
    {
        "company" => ascending ? query.OrderBy(a => a.Company.ToLower()) : query.OrderByDescending(a => a.Company.ToLower()),
        "appliedon" => ascending ? query.OrderBy(a => a.AppliedOn) : query.OrderByDescending(a => a.AppliedOn),
        // Priority is stored as text, so rank it explicitly: High > Medium > Low.
        "priority" => ascending
            ? query.OrderBy(a => a.Priority == Priority.High ? 2 : a.Priority == Priority.Medium ? 1 : 0)
            : query.OrderByDescending(a => a.Priority == Priority.High ? 2 : a.Priority == Priority.Medium ? 1 : 0),
        "created" => ascending ? query.OrderBy(a => a.CreatedAt) : query.OrderByDescending(a => a.CreatedAt),
        _ => ascending ? query.OrderBy(a => a.LastActivityAt) : query.OrderByDescending(a => a.LastActivityAt),
    };

    private static void Apply(JobApplication application, ApplicationFields fields)
    {
        application.Company = fields.Company.Trim();
        application.RoleTitle = fields.RoleTitle.Trim();
        application.JobUrl = Clean(fields.JobUrl);
        application.Source = fields.Source;
        application.Location = Clean(fields.Location);
        application.WorkMode = fields.WorkMode;
        application.SalaryMin = fields.SalaryMin;
        application.SalaryMax = fields.SalaryMax;
        application.Currency = fields.Currency.Trim().ToUpperInvariant();
        application.Sponsorship = fields.Sponsorship;
        application.Priority = fields.Priority;
        application.Notes = Clean(fields.Notes);
    }

    private static void EnsureNotInFuture(DateOnly? appliedOn, DateOnly today)
    {
        if (appliedOn is { } date && date > today)
        {
            throw new BadRequestException("The applied date cannot be in the future.");
        }
    }
}
