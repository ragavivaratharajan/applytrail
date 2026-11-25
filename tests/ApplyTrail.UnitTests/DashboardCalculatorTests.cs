using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using ApplyTrail.Domain;
using static ApplyTrail.UnitTests.TestData;

namespace ApplyTrail.UnitTests;

public class DashboardCalculatorTests
{
    private static DashboardSummaryDto Compute(params JobApplication[] applications) =>
        DashboardCalculator.Compute(applications, UserSettings.Default, Today, Now);

    /// <summary>A saved application carrying exactly one reminder, so each test controls its own data.</summary>
    private static JobApplication WithReminder(
        string company, DateOnly dueOn, string title = "Do the thing",
        ReminderType type = ReminderType.Deadline, TimeOnly? time = null)
    {
        var app = App(company, status: ApplicationStatus.Saved);
        app.AddReminder(type, title, dueOn, time);
        return app;
    }

    [Fact]
    public void Reminders_are_split_into_overdue_today_and_upcoming()
    {
        var summary = Compute(
            WithReminder("A", Today.AddDays(-2), "overdue"),
            WithReminder("B", Today, "today"),
            WithReminder("C", Today.AddDays(1), "tomorrow"),
            WithReminder("D", Today.AddDays(7), "edge of window"));

        Assert.Equal(new[] { "overdue" }, summary.Overdue.Select(r => r.Title));
        Assert.Equal(new[] { "today" }, summary.Today.Select(r => r.Title));
        Assert.Equal(new[] { "tomorrow", "edge of window" }, summary.Upcoming.Select(r => r.Title));
    }

    [Fact]
    public void Reminders_beyond_the_upcoming_window_are_not_shown()
    {
        var summary = Compute(WithReminder("A", Today.AddDays(DashboardCalculator.UpcomingWindowDays + 1)));

        Assert.Empty(summary.Overdue);
        Assert.Empty(summary.Today);
        Assert.Empty(summary.Upcoming);
    }

    [Fact]
    public void Reminders_carry_the_company_and_role_for_display()
    {
        var summary = Compute(WithReminder("Globex", Today));

        var reminder = Assert.Single(summary.Today);
        Assert.Equal("Globex", reminder.Company);
        Assert.Equal("Software Engineer", reminder.RoleTitle);
    }

    [Fact]
    public void Same_day_reminders_are_ordered_by_time_with_untimed_last()
    {
        var summary = Compute(
            WithReminder("Zulu", Today, "untimed"),
            WithReminder("Bravo", Today, "afternoon", ReminderType.Interview, new TimeOnly(14, 0)),
            WithReminder("Alpha", Today, "morning", ReminderType.Interview, new TimeOnly(9, 30)));

        Assert.Equal(new[] { "morning", "afternoon", "untimed" }, summary.Today.Select(r => r.Title));
    }

    [Fact]
    public void Archived_applications_do_not_nag_but_still_count_toward_the_weekly_goal()
    {
        var archived = App("Archived Co"); // applied today, follow-up scheduled
        archived.Archive(Now);

        var summary = Compute(archived);

        Assert.Empty(summary.Today);
        Assert.Empty(summary.Upcoming);
        Assert.Equal(1, summary.Goal.AppliedThisWeek);
        Assert.All(summary.StatusCounts.Values, count => Assert.Equal(0, count));
    }

    [Fact]
    public void Finished_applications_have_no_reminders()
    {
        var rejected = App("Rejected Co");
        rejected.ChangeStatus(ApplicationStatus.Rejected, Now, Today, 7);

        var summary = Compute(rejected);

        Assert.Empty(summary.Overdue);
        Assert.Empty(summary.Today);
        Assert.Empty(summary.Upcoming);
    }

    [Theory]
    [InlineData(8, false)]  // Sunday before this week
    [InlineData(9, true)]   // Monday
    [InlineData(15, true)]  // Sunday
    [InlineData(16, false)] // next Monday
    public void The_weekly_goal_counts_applications_from_monday_to_sunday(int dayOfNovember, bool counted)
    {
        var summary = Compute(App(appliedOn: new DateOnly(2026, 11, dayOfNovember)));

        Assert.Equal(counted ? 1 : 0, summary.Goal.AppliedThisWeek);
        Assert.Equal(UserSettings.Default.WeeklyGoal, summary.Goal.Target);
    }

    [Fact]
    public void Saved_applications_do_not_count_toward_the_weekly_goal()
    {
        var summary = Compute(App(status: ApplicationStatus.Saved));

        Assert.Equal(0, summary.Goal.AppliedThisWeek);
    }

    [Fact]
    public void Status_counts_list_every_status_in_pipeline_order()
    {
        var summary = Compute(
            App("A"),
            App("B"),
            App("C", status: ApplicationStatus.Saved));

        Assert.Equal(
            new[] { "Saved", "Applied", "Screening", "Interviewing", "Offer", "Rejected", "Withdrawn", "Ghosted" },
            summary.StatusCounts.Keys);
        Assert.Equal(1, summary.StatusCounts["Saved"]);
        Assert.Equal(2, summary.StatusCounts["Applied"]);
        Assert.Equal(0, summary.StatusCounts["Offer"]);
    }

    [Fact]
    public void Stale_applications_are_listed_longest_quiet_first_and_exclude_archived_and_finished()
    {
        var quiet12 = App("Hotel", createdAt: Now.AddDays(-12));
        var quiet5 = App("Juliet", createdAt: Now.AddDays(-5));
        var quiet30 = App("India", createdAt: Now.AddDays(-30));
        var archived = App("Kilo", createdAt: Now.AddDays(-40));
        archived.Archive(Now);
        var offer = App("Lima", createdAt: Now.AddDays(-40));
        offer.ChangeStatus(ApplicationStatus.Offer, Now.AddDays(-40), Today.AddDays(-40), 7);

        var summary = Compute(quiet12, quiet5, quiet30, archived, offer);

        Assert.Equal(new[] { "India", "Hotel" }, summary.Stale.Select(s => s.Company));
        Assert.Equal(new[] { 30, 12 }, summary.Stale.Select(s => s.DaysSinceActivity));
    }

    [Fact]
    public void An_empty_tracker_produces_an_empty_but_valid_dashboard()
    {
        var summary = Compute();

        Assert.Empty(summary.Overdue);
        Assert.Empty(summary.Today);
        Assert.Empty(summary.Upcoming);
        Assert.Empty(summary.Stale);
        Assert.Equal(0, summary.Goal.AppliedThisWeek);
        Assert.Equal(8, summary.StatusCounts.Count);
    }
}
