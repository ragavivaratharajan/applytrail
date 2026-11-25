using System.Net;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Domain;

namespace ApplyTrail.IntegrationTests;

public class RemindersDashboardTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string TodayQuery => $"today={Api.Today:yyyy-MM-dd}";

    [Fact]
    public async Task The_dashboard_buckets_reminders_and_counts_statuses()
    {
        var user = await factory.RegisterAsync();
        var old = await user.CreateAppAsync("Old Co", "Dev", r => r.AppliedOn = Api.Today.AddDays(-10)); // follow-up clamps to today
        await user.CreateAppAsync("Fresh Co");                                                              // follow-up in 7 days
        await (await user.Client.PostJsonAsync($"/api/applications/{old.Id}/reminders",
            new CreateReminderRequest { Type = ReminderType.Deadline, Title = "Send CV", DueOn = Api.Today.AddDays(-2) })).ExpectAsync(HttpStatusCode.Created);

        var response = await user.Client.GetAsync($"/api/dashboard?{TodayQuery}");

        await response.ExpectAsync(HttpStatusCode.OK);
        var dashboard = await response.ReadAsync<DashboardSummaryDto>();
        Assert.Equal(new[] { "Send CV" }, dashboard.Overdue.Select(r => r.Title).ToArray());
        Assert.Equal(new[] { "Old Co" }, dashboard.Today.Select(r => r.Company).ToArray());
        Assert.Equal(new[] { "Fresh Co" }, dashboard.Upcoming.Select(r => r.Company).ToArray());
        Assert.Equal(2, dashboard.StatusCounts["Applied"]);
        Assert.Equal(10, dashboard.Goal.Target);
        Assert.True(dashboard.Goal.AppliedThisWeek >= 1);
    }

    [Fact]
    public async Task Completing_a_follow_up_logs_it_and_schedules_the_next_nudge()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme", "Dev", r => r.AppliedOn = Api.Today.AddDays(-10));
        var due = (await (await user.Client.GetAsync($"/api/reminders?bucket=Today&{TodayQuery}")).ReadAsync<List<ReminderDto>>()).Single();

        var response = await user.Client.PostJsonAsync($"/api/applications/{app.Id}/reminders/{due.Id}/complete",
            new CompleteReminderRequest { LogAsEvent = true, Note = "Emailed Sam", RemindAgainInDays = 5, Today = Api.Today });

        await response.ExpectAsync(HttpStatusCode.OK);
        Assert.NotNull((await response.ReadAsync<ReminderDto>()).CompletedAt);

        var detail = await (await user.Client.GetAsync($"/api/applications/{app.Id}")).ReadAsync<ApplicationDetailDto>();
        Assert.Contains(detail.Events, e => e.Type == ActivityType.Email && e.Title == "Followed up" && e.Details == "Emailed Sam");
        Assert.Contains(detail.Reminders, r => r.CompletedAt is null && r.DueOn == Api.Today.AddDays(5));

        // completing again is harmless
        await (await user.Client.PostJsonAsync($"/api/applications/{app.Id}/reminders/{due.Id}/complete",
            new CompleteReminderRequest { Today = Api.Today })).ExpectAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Snoozing_moves_a_reminder_to_today_plus_n_days_and_closed_reminders_cannot_be_snoozed()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme");
        var reminder = (await (await user.Client.GetAsync("/api/reminders")).ReadAsync<List<ReminderDto>>()).Single();

        var snoozed = await (await user.Client.PostJsonAsync($"/api/applications/{app.Id}/reminders/{reminder.Id}/snooze",
            new SnoozeReminderRequest { Days = 3, Today = Api.Today })).ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(Api.Today.AddDays(3), (await snoozed.ReadAsync<ReminderDto>()).DueOn);

        await user.Client.PostJsonAsync($"/api/applications/{app.Id}/reminders/{reminder.Id}/complete", new CompleteReminderRequest { Today = Api.Today });
        await (await user.Client.PostJsonAsync($"/api/applications/{app.Id}/reminders/{reminder.Id}/snooze",
            new SnoozeReminderRequest { Days = 2 })).ExpectAsync(HttpStatusCode.Conflict);
        await (await user.Client.PostJsonAsync($"/api/applications/{app.Id}/reminders/{Guid.NewGuid()}/snooze",
            new SnoozeReminderRequest { Days = 2 })).ExpectAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reminders_of_finished_or_archived_applications_disappear()
    {
        var user = await factory.RegisterAsync();
        var rejected = await user.CreateAppAsync("Rejected Co");
        var archived = await user.CreateAppAsync("Archived Co");
        await user.CreateAppAsync("Live Co");

        await user.Client.PostJsonAsync($"/api/applications/{rejected.Id}/status", new ChangeStatusRequest { Status = ApplicationStatus.Rejected, Today = Api.Today });
        await user.Client.PostAsync($"/api/applications/{archived.Id}/archive", content: null);

        var reminders = await (await user.Client.GetAsync("/api/reminders")).ReadAsync<List<ReminderDto>>();
        Assert.Equal(new[] { "Live Co" }, reminders.Select(r => r.Company).ToArray());
    }

    [Fact]
    public async Task A_client_date_that_is_wildly_wrong_is_rejected_instead_of_corrupting_reminders()
    {
        var user = await factory.RegisterAsync();

        await (await user.Client.GetAsync("/api/dashboard?today=2020-01-01")).ExpectAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Insights_report_the_funnel_and_the_requested_number_of_weeks()
    {
        var user = await factory.RegisterAsync();
        var a = await user.CreateAppAsync("A", "Dev", r => { r.Source = JobSource.LinkedIn; r.AppliedOn = Api.Today.AddDays(-3); });
        await user.Client.PostJsonAsync($"/api/applications/{a.Id}/status", new ChangeStatusRequest { Status = ApplicationStatus.Interviewing, Today = Api.Today });
        await user.CreateAppAsync("B", "Dev", r => r.Source = JobSource.Indeed);

        var response = await user.Client.GetAsync($"/api/insights?weeks=4&{TodayQuery}");

        await response.ExpectAsync(HttpStatusCode.OK);
        var insights = await response.ReadAsync<InsightsDto>();
        Assert.Equal(new FunnelDto(Applied: 2, Responded: 1, Interviewed: 1, Offers: 0), insights.Funnel);
        Assert.Equal(0.5, insights.ResponseRate);
        Assert.Equal(4, insights.Weekly.Count);
        Assert.Equal(2, insights.BySource.Count);
    }

    [Fact]
    public async Task Settings_change_the_goal_the_follow_up_delay_and_are_validated()
    {
        var user = await factory.RegisterAsync();

        await (await user.Client.PutJsonAsync("/api/me/settings", new UpdateSettingsRequest { WeeklyGoal = 0 })).ExpectAsync(HttpStatusCode.BadRequest);

        var updated = await (await user.Client.PutJsonAsync("/api/me/settings",
            new UpdateSettingsRequest { WeeklyGoal = 15, FollowUpAfterDays = 3, StaleAfterDays = 5 })).ExpectAsync(HttpStatusCode.OK);
        Assert.Equal(15, (await updated.ReadAsync<UserSettingsDto>()).WeeklyGoal);

        var app = await user.CreateAppAsync("Quick Follow-up");
        Assert.Equal(Api.Today.AddDays(3), app.NextReminderOn);
        Assert.Equal(15, (await (await user.Client.GetAsync($"/api/dashboard?{TodayQuery}")).ReadAsync<DashboardSummaryDto>()).Goal.Target);
    }
}
