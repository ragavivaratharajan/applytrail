using ApplyTrail.Application.Dtos;
using ApplyTrail.Domain;

namespace ApplyTrail.Application.Logic;

/// <summary>
/// Builds the "what do I do today?" screen. Pure: give it applications (with reminders loaded)
/// and the user's calendar date, get the answer.
/// </summary>
public static class DashboardCalculator
{
    public const int UpcomingWindowDays = 7;

    public static DashboardSummaryDto Compute(
        IReadOnlyCollection<JobApplication> applications,
        UserSettings settings,
        DateOnly today,
        DateTime nowUtc)
    {
        // Goal progress counts everything applied this week, archived or not:
        // tidying the board must never make last week's effort disappear.
        var weekStart = WeekMath.StartOfWeek(today);
        var weekEnd = weekStart.AddDays(7);
        var appliedThisWeek = applications.Count(a => a.AppliedOn is { } d && d >= weekStart && d < weekEnd);

        // Archived applications are hidden on purpose, so they neither nag nor show up in counts.
        var live = applications.Where(a => !a.IsArchived).ToList();

        var open = live
            .SelectMany(a => a.Reminders.Where(r => r.IsOpen).Select(r => (Application: a, Reminder: r)))
            .OrderBy(x => x.Reminder.DueOn)
            .ThenBy(x => x.Reminder.TimeOfDay ?? TimeOnly.MaxValue)
            .ThenBy(x => x.Application.Company, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var horizon = today.AddDays(UpcomingWindowDays);

        List<ReminderDto> Bucket(Func<DateOnly, bool> predicate) => open
            .Where(x => predicate(x.Reminder.DueOn))
            .Select(x => x.Reminder.ToDto(x.Application))
            .ToList();

        var stale = live
            .Where(a => a.IsStale(nowUtc, settings.StaleAfterDays))
            .Select(a => new StaleApplicationDto(
                a.Id, a.Company, a.RoleTitle, a.Status, (int)Math.Floor((nowUtc - a.LastActivityAt).TotalDays)))
            .OrderByDescending(s => s.DaysSinceActivity)
            .ThenBy(s => s.Company, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var counts = new Dictionary<string, int>();
        foreach (var status in Enum.GetValues<ApplicationStatus>())
        {
            counts[status.ToString()] = live.Count(a => a.Status == status);
        }

        return new DashboardSummaryDto(
            new GoalProgressDto(settings.WeeklyGoal, appliedThisWeek),
            Bucket(due => due < today),
            Bucket(due => due == today),
            Bucket(due => due > today && due <= horizon),
            stale,
            counts);
    }
}
