using ApplyTrail.Application.Abstractions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using Microsoft.EntityFrameworkCore;
using static ApplyTrail.Application.Services.ServiceHelpers;

namespace ApplyTrail.Application.Services;

/// <summary>
/// Loads a user's applications and hands them to the pure calculators. A job search produces hundreds
/// of rows, not millions, so doing the maths in memory is simpler and easier to test than SQL.
/// </summary>
public sealed class DashboardService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    public const int MaxWeeks = 26;

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? todayFromClient, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var now = clock.GetUtcNow().UtcDateTime;
        var today = ResolveToday(todayFromClient, now);
        var settings = await db.GetSettingsAsync(userId, ct);

        var applications = await db.Applications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Reminders)
            .ToListAsync(ct);

        return DashboardCalculator.Compute(applications, settings, today, now);
    }

    public async Task<InsightsDto> GetInsightsAsync(int weeks, DateOnly? todayFromClient, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var now = clock.GetUtcNow().UtcDateTime;
        var today = ResolveToday(todayFromClient, now);
        var settings = await db.GetSettingsAsync(userId, ct);

        var applications = await db.Applications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .Include(a => a.Events)
            .ToListAsync(ct);

        return InsightsCalculator.Compute(applications, settings, today, now, Math.Clamp(weeks, 1, MaxWeeks));
    }
}
