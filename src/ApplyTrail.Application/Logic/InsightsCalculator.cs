using ApplyTrail.Application.Dtos;
using ApplyTrail.Domain;

namespace ApplyTrail.Application.Logic;

/// <summary>
/// Turns the application history into honest numbers. Definitions (shown in the UI too):
/// <list type="bullet">
///   <item><b>Applied</b>: has an applied date (anything that has ever left Saved).</item>
///   <item><b>Responded</b>: is, or ever was, Screening / Interviewing / Offer / Rejected.</item>
///   <item><b>Interviewed</b>: is, or ever was, Interviewing or Offer.</item>
///   <item><b>Days to response</b>: applied date to the first real status change into a response status.
///   Applications entered directly in a later stage have no such change and are left out of this average.</item>
/// </list>
/// Archived applications are included: archiving tidies the board, it does not rewrite history.
/// </summary>
public static class InsightsCalculator
{
    public const int DefaultWeeks = 8;

    public static InsightsDto Compute(
        IReadOnlyCollection<JobApplication> applications,
        UserSettings settings,
        DateOnly today,
        DateTime nowUtc,
        int weeks = DefaultWeeks)
    {
        var applied = applications.Where(a => a.AppliedOn is not null).ToList();
        var responded = applied.Where(HasResponded).ToList();
        var interviewed = applied.Where(HasInterviewed).ToList();
        var offers = applied.Count(HasOffer);

        var funnel = new FunnelDto(applied.Count, responded.Count, interviewed.Count, offers);

        var firstResponses = applied
            .Select(a => (Application: a, At: FirstResponseAt(a)))
            .Where(x => x.At is not null)
            .Select(x => (x.Application, Date: DateOnly.FromDateTime(x.At!.Value)))
            .ToList();

        double? averageDays = firstResponses.Count == 0
            ? null
            : Math.Round(
                firstResponses.Average(x => Math.Max(0, x.Date.DayNumber - x.Application.AppliedOn!.Value.DayNumber)),
                1);

        var thisWeek = WeekMath.StartOfWeek(today);
        var weekly = new List<WeeklyPointDto>();
        for (var i = Math.Max(1, weeks) - 1; i >= 0; i--)
        {
            var start = thisWeek.AddDays(-7 * i);
            var end = start.AddDays(7);
            weekly.Add(new WeeklyPointDto(
                start,
                applied.Count(a => a.AppliedOn!.Value >= start && a.AppliedOn.Value < end),
                firstResponses.Count(x => x.Date >= start && x.Date < end)));
        }

        var bySource = applied
            .GroupBy(a => a.Source)
            .Select(g => new SourceStatDto(g.Key, g.Count(), g.Count(HasResponded), g.Count(HasInterviewed)))
            .OrderByDescending(s => s.Applied)
            .ThenBy(s => s.Source)
            .ToList();

        return new InsightsDto(
            funnel,
            Rate(responded.Count, applied.Count),
            Rate(interviewed.Count, applied.Count),
            averageDays,
            weekly,
            bySource,
            applications.Count(a => a.IsStale(nowUtc, settings.StaleAfterDays)));
    }

    private static double Rate(int part, int whole) => whole == 0 ? 0 : Math.Round((double)part / whole, 4);

    private static bool EverMovedTo(JobApplication a, Func<ApplicationStatus, bool> predicate) =>
        a.Events.Any(e => e.Type == ActivityType.StatusChange && e.ToStatus is { } s && predicate(s));

    private static bool HasResponded(JobApplication a) => a.Status.IsResponse() || EverMovedTo(a, s => s.IsResponse());

    private static bool HasInterviewed(JobApplication a) =>
        a.Status.IsInterviewStage() || EverMovedTo(a, s => s.IsInterviewStage());

    private static bool HasOffer(JobApplication a) =>
        a.Status == ApplicationStatus.Offer || EverMovedTo(a, s => s == ApplicationStatus.Offer);

    private static DateTime? FirstResponseAt(JobApplication a) => a.Events
        .Where(e => e.Type == ActivityType.StatusChange
                    && e.FromStatus is not null // skip "Added as X": that is not a real transition
                    && e.ToStatus is { } s && s.IsResponse())
        .Select(e => (DateTime?)e.OccurredAt)
        .Min();
}
