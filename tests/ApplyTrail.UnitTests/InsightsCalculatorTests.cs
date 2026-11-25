using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using ApplyTrail.Domain;
using static ApplyTrail.UnitTests.TestData;

namespace ApplyTrail.UnitTests;

public class InsightsCalculatorTests
{
    private static readonly DateOnly Oct28 = new(2026, 10, 28);
    private static readonly DateOnly Nov2 = new(2026, 11, 2);
    private static readonly DateOnly Nov3 = new(2026, 11, 3);
    private static readonly DateOnly Nov9 = new(2026, 11, 9);
    private static readonly DateOnly Nov10 = new(2026, 11, 10);
    private static readonly DateOnly Nov11 = new(2026, 11, 11);

    /// <summary>
    /// Seven applications with a known history:
    /// A  LinkedIn  applied 2 Nov, Screening 6 Nov, Interviewing 10 Nov, Offer 12 Nov     (response after 4 days)
    /// B  LinkedIn  applied 3 Nov, Rejected 10 Nov                                         (response after 7 days)
    /// C  Indeed    applied 10 Nov, silence
    /// D  Indeed    applied 11 Nov, Ghosted 12 Nov                                         (never answered)
    /// E  Referral  entered directly as Screening, applied 9 Nov                           (no real transition)
    /// F  saved only, never applied
    /// G  LinkedIn  applied 28 Oct, Screening 1 Nov, Ghosted 12 Nov                        (response after 4 days)
    /// </summary>
    private static List<JobApplication> Scenario()
    {
        var e = App("E", status: ApplicationStatus.Screening, appliedOn: Nov9, createdAt: At(11, 9));
        e.Source = JobSource.Referral;

        return
        [
            WithHistory("A", JobSource.LinkedIn, Nov2,
                (ApplicationStatus.Screening, At(11, 6)),
                (ApplicationStatus.Interviewing, At(11, 10)),
                (ApplicationStatus.Offer, At(11, 12))),
            WithHistory("B", JobSource.LinkedIn, Nov3, (ApplicationStatus.Rejected, At(11, 10))),
            WithHistory("C", JobSource.Indeed, Nov10),
            WithHistory("D", JobSource.Indeed, Nov11, (ApplicationStatus.Ghosted, At(11, 12))),
            e,
            App("F", status: ApplicationStatus.Saved),
            WithHistory("G", JobSource.LinkedIn, Oct28,
                (ApplicationStatus.Screening, At(11, 1)),
                (ApplicationStatus.Ghosted, At(11, 12))),
        ];
    }

    private static InsightsDto Compute(IReadOnlyCollection<JobApplication> applications, int weeks = 3) =>
        InsightsCalculator.Compute(applications, UserSettings.Default, Today, Now, weeks);

    [Fact]
    public void The_funnel_counts_what_ever_happened_not_just_where_things_ended_up()
    {
        var insights = Compute(Scenario());

        // Applied: A B C D E G. Responded: A B E G (G was ghosted AFTER it answered). Interviewed: A. Offers: A.
        Assert.Equal(new FunnelDto(Applied: 6, Responded: 4, Interviewed: 1, Offers: 1), insights.Funnel);
    }

    [Fact]
    public void Rates_are_fractions_of_everything_applied_to()
    {
        var insights = Compute(Scenario());

        Assert.Equal(0.6667, insights.ResponseRate, 4);
        Assert.Equal(0.1667, insights.InterviewRate, 4);
    }

    [Fact]
    public void Average_days_to_response_uses_real_transitions_only()
    {
        var insights = Compute(Scenario());

        // A: 2 Nov -> 6 Nov = 4. B: 3 Nov -> 10 Nov = 7. G: 28 Oct -> 1 Nov = 4. E is excluded (entered at Screening).
        Assert.Equal(5.0, insights.AverageDaysToResponse!.Value);
    }

    [Fact]
    public void The_weekly_series_ends_with_the_current_week_and_starts_on_mondays()
    {
        var insights = Compute(Scenario());

        Assert.Equal(
            new[] { new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 9) },
            insights.Weekly.Select(w => w.WeekStart));
        Assert.Equal(new[] { 1, 2, 3 }, insights.Weekly.Select(w => w.Applied));
        Assert.Equal(new[] { 1, 1, 1 }, insights.Weekly.Select(w => w.Responded));
    }

    [Fact]
    public void Sources_are_ranked_by_volume()
    {
        var insights = Compute(Scenario());

        Assert.Equal(
            new[]
            {
                new SourceStatDto(JobSource.LinkedIn, Applied: 3, Responded: 3, Interviewed: 1),
                new SourceStatDto(JobSource.Indeed, Applied: 2, Responded: 0, Interviewed: 0),
                new SourceStatDto(JobSource.Referral, Applied: 1, Responded: 1, Interviewed: 0),
            },
            insights.BySource);
    }

    [Fact]
    public void Archiving_does_not_change_the_history()
    {
        var applications = Scenario();
        foreach (var application in applications)
        {
            application.Archive(Now);
        }

        var insights = Compute(applications);

        Assert.Equal(new FunnelDto(6, 4, 1, 1), insights.Funnel);
    }

    [Fact]
    public void Quiet_applications_are_counted_but_archived_ones_are_not()
    {
        var quiet = WithHistory("Quiet", JobSource.LinkedIn, new DateOnly(2026, 10, 20));
        var hidden = WithHistory("Hidden", JobSource.LinkedIn, new DateOnly(2026, 10, 20));
        hidden.Archive(Now);

        var insights = Compute([quiet, hidden]);

        Assert.Equal(1, insights.StaleCount);
    }

    [Fact]
    public void With_no_applications_every_number_is_zero_and_nothing_divides_by_zero()
    {
        var insights = Compute([], weeks: 8);

        Assert.Equal(new FunnelDto(0, 0, 0, 0), insights.Funnel);
        Assert.Equal(0, insights.ResponseRate);
        Assert.Equal(0, insights.InterviewRate);
        Assert.Null(insights.AverageDaysToResponse);
        Assert.Equal(8, insights.Weekly.Count);
        Assert.All(insights.Weekly, w => Assert.Equal(0, w.Applied + w.Responded));
        Assert.Empty(insights.BySource);
    }

    [Fact]
    public void Applications_with_no_replies_have_no_average_instead_of_a_misleading_zero()
    {
        var insights = Compute([WithHistory("C", JobSource.Indeed, Nov10)]);

        Assert.Null(insights.AverageDaysToResponse);
        Assert.Equal(0, insights.ResponseRate);
    }
}
