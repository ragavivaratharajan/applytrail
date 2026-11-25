using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using ApplyTrail.Application.Dtos;
using ApplyTrail.Application.Logic;
using ApplyTrail.Domain;

namespace ApplyTrail.IntegrationTests;

public class ApplicationsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Creating_an_application_returns_201_with_a_location_and_persists_everything()
    {
        var user = await factory.RegisterAsync();

        var response = await user.Client.PostJsonAsync("/api/applications", new CreateApplicationRequest
        {
            Company = "Acme", RoleTitle = "Senior .NET Engineer", JobUrl = "https://example.com/jobs/1", Source = JobSource.LinkedIn,
            Location = "Dublin", WorkMode = WorkMode.Remote, SalaryMin = 70000, SalaryMax = 90000, Currency = "eur",
            Sponsorship = Sponsorship.Offered, Priority = Priority.High, Notes = "Referral from Sam", Today = Api.Today,
        });

        await response.ExpectAsync(HttpStatusCode.Created);
        var created = await response.ReadAsync<ApplicationDto>();
        Assert.EndsWith($"/api/applications/{created.Id}", response.Headers.Location?.ToString());

        var detail = await (await user.Client.GetAsync($"/api/applications/{created.Id}")).ExpectAsync(HttpStatusCode.OK);
        var loaded = await detail.ReadAsync<ApplicationDetailDto>();
        Assert.Equal("Senior .NET Engineer", loaded.Application.RoleTitle);
        Assert.Equal(JobSource.LinkedIn, loaded.Application.Source);
        Assert.Equal(WorkMode.Remote, loaded.Application.WorkMode);
        Assert.Equal("EUR", loaded.Application.Currency);
        Assert.Equal(Sponsorship.Offered, loaded.Application.Sponsorship);
        Assert.Equal(Api.Today, loaded.Application.AppliedOn);
        Assert.Equal(Api.Today.AddDays(7), loaded.Application.NextReminderOn);
        Assert.Single(loaded.Events, e => e.Title == "Added as Applied");
        Assert.Single(loaded.Reminders, r => r.Type == ReminderType.FollowUp && r.DueOn == Api.Today.AddDays(7));
    }

    [Fact]
    public async Task Invalid_input_is_rejected_with_a_message_for_each_problem_field()
    {
        var user = await factory.RegisterAsync();

        async Task<string> Post(Action<CreateApplicationRequest> configure)
        {
            var request = new CreateApplicationRequest { Company = "Acme", RoleTitle = "Dev", Today = Api.Today };
            configure(request);
            var response = await user.Client.PostJsonAsync("/api/applications", request);
            await response.ExpectAsync(HttpStatusCode.BadRequest);
            return await response.Content.ReadAsStringAsync();
        }

        Assert.Contains("company", await Post(r => r.Company = ""), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jobUrl", await Post(r => r.JobUrl = "javascript:alert(1)"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("salaryMax", await Post(r => { r.SalaryMin = 90000; r.SalaryMax = 80000; }), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("currency", await Post(r => r.Currency = "EURO"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("future", await Post(r => r.AppliedOn = Api.Today.AddDays(1)), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"company":"A","roleTitle":"B","status":99}""")]
    [InlineData("""{"company":"A","roleTitle":"B","status":1}""")]
    [InlineData("""{"company":"A","roleTitle":"B","status":"Bogus"}""")]
    [InlineData("""{"company":"A","roleTitle":"B","priority":"Urgent"}""")]
    [InlineData("""not json""")]
    public async Task Unknown_enum_values_and_malformed_json_are_a_400_not_a_500(string body)
    {
        var user = await factory.RegisterAsync();

        var response = await user.Client.PostAsync("/api/applications", new StringContent(body, Encoding.UTF8, "application/json"));

        await response.ExpectAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_route_id_that_is_not_a_guid_is_a_404()
    {
        var user = await factory.RegisterAsync();

        await (await user.Client.GetAsync("/api/applications/not-a-guid")).ExpectAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Moving_an_application_through_the_pipeline_is_logged_and_stops_the_follow_up()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme");
        Assert.NotNull(app.NextReminderOn);

        var moved = await (await user.Client.PostJsonAsync($"/api/applications/{app.Id}/status",
            new ChangeStatusRequest { Status = ApplicationStatus.Screening, Note = "Recruiter call booked", Today = Api.Today }))
            .ExpectAsync(HttpStatusCode.OK);

        var dto = await moved.ReadAsync<ApplicationDto>();
        Assert.Equal(ApplicationStatus.Screening, dto.Status);
        Assert.Null(dto.NextReminderOn);

        var detail = await (await user.Client.GetAsync($"/api/applications/{app.Id}")).ReadAsync<ApplicationDetailDto>();
        Assert.Contains(detail.Events, e => e.Title == "Moved to Screening" && e.Details == "Recruiter call booked");
    }

    [Fact]
    public async Task Updating_changes_the_fields_and_keeps_the_history()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme");

        var response = await user.Client.PutJsonAsync($"/api/applications/{app.Id}",
            new UpdateApplicationRequest { Company = "Acme Ireland", RoleTitle = "Lead Engineer", Priority = Priority.High, Notes = "  new note " });

        await response.ExpectAsync(HttpStatusCode.OK);
        var dto = await response.ReadAsync<ApplicationDto>();
        Assert.Equal("Acme Ireland", dto.Company);
        Assert.Equal("new note", dto.Notes);
        Assert.Equal(Api.Today, dto.AppliedOn);
    }

    [Fact]
    public async Task Search_filters_sorting_and_paging_work_against_the_real_database()
    {
        var user = await factory.RegisterAsync();
        await user.CreateAppAsync("zeta Corp", "Backend Dev", r => { r.Source = JobSource.Indeed; r.Priority = Priority.Low; r.Location = "Cork"; });
        await user.CreateAppAsync("Alpha Systems", "Angular Developer", r => { r.Source = JobSource.LinkedIn; r.Priority = Priority.High; r.Notes = "Great visa support"; r.Sponsorship = Sponsorship.Offered; });
        var mid = await user.CreateAppAsync("Mid Labs", "Full Stack", r => { r.Source = JobSource.LinkedIn; r.Priority = Priority.Medium; r.Location = "Dublin"; });
        await user.Client.PostJsonAsync($"/api/applications/{mid.Id}/status", new ChangeStatusRequest { Status = ApplicationStatus.Interviewing, Today = Api.Today });
        var archived = await user.CreateAppAsync("Archived Inc");
        await user.Client.PostAsync($"/api/applications/{archived.Id}/archive", content: null);

        async Task<PagedResult<ApplicationDto>> List(string query)
        {
            var response = await user.Client.GetAsync("/api/applications" + query);
            await response.ExpectAsync(HttpStatusCode.OK);
            return await response.ReadAsync<PagedResult<ApplicationDto>>();
        }

        string[] Names(PagedResult<ApplicationDto> page) => page.Items.Select(i => i.Company).ToArray();

        Assert.Equal(new[] { "Alpha Systems", "Mid Labs", "zeta Corp" }, Names(await List("?sort=company&dir=asc")));       // case-insensitive
        Assert.Equal(new[] { "zeta Corp" }, Names(await List("?q=ZETA")));                                                   // search ignores case
        Assert.Equal(new[] { "Alpha Systems" }, Names(await List("?q=visa")));                                               // ...and looks in notes
        Assert.Equal(new[] { "zeta Corp" }, Names(await List("?q=cork")));                                                   // ...and location
        Assert.Equal(new[] { "Mid Labs" }, Names(await List("?status=Interviewing")));
        Assert.Equal(3, (await List("?status=Applied&status=Interviewing")).Total);                                   // repeated query parameters
        Assert.Equal(2, (await List("?source=LinkedIn")).Total);
        Assert.Equal(new[] { "Alpha Systems" }, Names(await List("?sponsorship=Offered")));
        Assert.Equal(new[] { Priority.High, Priority.Medium, Priority.Low },
            (await List("?sort=priority&dir=desc")).Items.Select(i => i.Priority).ToArray());                         // High > Medium > Low
        Assert.Equal(new[] { "Archived Inc" }, Names(await List("?archived=true")));

        var page2 = await List("?sort=company&dir=asc&pageSize=2&page=2");
        Assert.Equal(new[] { "zeta Corp" }, Names(page2));
        Assert.Equal(3, page2.Total);

        await (await user.Client.GetAsync("/api/applications?sort=bogus")).ExpectAsync(HttpStatusCode.BadRequest);
        await (await user.Client.GetAsync("/api/applications?pageSize=500")).ExpectAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Archiving_hides_an_application_and_unarchiving_restores_it()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme");

        await (await user.Client.PostAsync($"/api/applications/{app.Id}/archive", content: null)).ExpectAsync(HttpStatusCode.OK);
        var hidden = await (await user.Client.GetAsync("/api/applications")).ReadAsync<PagedResult<ApplicationDto>>();
        Assert.Empty(hidden.Items);

        var restored = await (await user.Client.PostAsync($"/api/applications/{app.Id}/unarchive", content: null)).ReadAsync<ApplicationDto>();
        Assert.False(restored.IsArchived);
        Assert.Single((await (await user.Client.GetAsync("/api/applications")).ReadAsync<PagedResult<ApplicationDto>>()).Items);
    }

    [Fact]
    public async Task Deleting_an_application_removes_it_together_with_its_events_and_reminders()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme");
        await (await user.Client.PostJsonAsync($"/api/applications/{app.Id}/events",
            new CreateEventRequest { Type = ApplicationActivity.Email, Title = "Recruiter emailed" })).ExpectAsync(HttpStatusCode.Created);

        await (await user.Client.DeleteAsync($"/api/applications/{app.Id}")).ExpectAsync(HttpStatusCode.NoContent);

        await (await user.Client.GetAsync($"/api/applications/{app.Id}")).ExpectAsync(HttpStatusCode.NotFound);
        var reminders = await (await user.Client.GetAsync("/api/reminders")).ReadAsync<List<ReminderDto>>();
        Assert.DoesNotContain(reminders, r => r.ApplicationId == app.Id);
    }

    [Fact]
    public async Task Logged_events_appear_newest_first_and_employer_contact_moves_the_activity_clock()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme");
        factory.Clock.Advance(TimeSpan.FromSeconds(1)); // the test clock is frozen; a real call happens later than "Added as Applied"

        var response = await user.Client.PostJsonAsync($"/api/applications/{app.Id}/events",
            new CreateEventRequest { Type = ApplicationActivity.PhoneScreen, Title = "Recruiter call", Details = "30 minutes" });

        await response.ExpectAsync(HttpStatusCode.Created);
        var detail = await (await user.Client.GetAsync($"/api/applications/{app.Id}")).ReadAsync<ApplicationDetailDto>();
        Assert.Equal("Recruiter call", detail.Events[0].Title);
        Assert.True(detail.Application.LastActivityAt >= app.LastActivityAt);

        await (await user.Client.PostJsonAsync($"/api/applications/{app.Id}/events",
            new { type = "StatusChange", title = "sneaky" })).ExpectAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Timestamps_leave_the_api_as_utc_even_after_a_round_trip_through_sqlite()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme");

        var json = await (await user.Client.GetAsync($"/api/applications/{app.Id}")).Content.ReadAsStringAsync();

        // A missing "Z" would make a browser read the time as local, shifting it by the user's UTC offset.
        Assert.Matches(new Regex("\"createdAt\":\"[^\"]+Z\""), json);
        Assert.Matches(new Regex("\"lastActivityAt\":\"[^\"]+Z\""), json);
        Assert.Matches(new Regex("\"occurredAt\":\"[^\"]+Z\""), json);
    }

    [Fact]
    public async Task Duplicate_check_recognises_the_same_job_through_a_different_tracking_link()
    {
        var user = await factory.RegisterAsync();
        var app = await user.CreateAppAsync("Acme", "Platform Engineer", r => r.JobUrl = "https://www.linkedin.com/jobs/view/777/?trackingId=abc");

        var response = await user.Client.PostJsonAsync("/api/applications/check-duplicates",
            new DuplicateCheckRequest { Company = "Whatever", RoleTitle = "x", JobUrl = "https://linkedin.com/jobs/view/777?utm_source=share" });

        await response.ExpectAsync(HttpStatusCode.OK);
        var match = Assert.Single(await response.ReadAsync<List<DuplicateMatchDto>>());
        Assert.Equal(app.Id, match.Id);
        Assert.Equal(DuplicateReason.SameLink, match.Reason);
    }

    [Fact]
    public async Task One_user_can_never_see_or_change_another_users_application()
    {
        var alice = await factory.RegisterAsync();
        var bob = await factory.RegisterAsync();
        var app = await alice.CreateAppAsync("Alice's Secret Co");
        var reminder = (await (await alice.Client.GetAsync("/api/reminders")).ReadAsync<List<ReminderDto>>()).First();

        var id = app.Id;
        await (await bob.Client.GetAsync($"/api/applications/{id}")).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.PutJsonAsync($"/api/applications/{id}", new UpdateApplicationRequest { Company = "Hijacked", RoleTitle = "x" })).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.PostJsonAsync($"/api/applications/{id}/status", new ChangeStatusRequest { Status = ApplicationStatus.Offer })).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.PostAsync($"/api/applications/{id}/archive", content: null)).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.PostJsonAsync($"/api/applications/{id}/events", new CreateEventRequest { Title = "x" })).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.PostJsonAsync($"/api/applications/{id}/reminders", new CreateReminderRequest { Title = "x", DueOn = Api.Today })).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.PostJsonAsync($"/api/applications/{id}/reminders/{reminder.Id}/complete", new CompleteReminderRequest())).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.PostJsonAsync($"/api/applications/{id}/reminders/{reminder.Id}/snooze", new SnoozeReminderRequest())).ExpectAsync(HttpStatusCode.NotFound);
        await (await bob.Client.DeleteAsync($"/api/applications/{id}")).ExpectAsync(HttpStatusCode.NotFound);

        Assert.Empty((await (await bob.Client.GetAsync("/api/applications")).ReadAsync<PagedResult<ApplicationDto>>()).Items);
        Assert.Empty(await (await bob.Client.GetAsync("/api/reminders")).ReadAsync<List<ReminderDto>>());
        Assert.DoesNotContain("Alice's Secret Co", await (await bob.Client.GetAsync("/api/export/csv")).Content.ReadAsStringAsync());

        var intact = await (await alice.Client.GetAsync($"/api/applications/{id}")).ReadAsync<ApplicationDetailDto>();
        Assert.Equal("Alice's Secret Co", intact.Application.Company);
    }
}
