using ApplyTrail.Application.Abstractions;
using ApplyTrail.Domain;
using ApplyTrail.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApplyTrail.Infrastructure.Seeding;

/// <summary>
/// Creates a demo account full of realistic (and entirely fictional) applications, dated relative to "today",
/// so the dashboard, board and insights have something to show the first time the app starts.
/// Safe to run on every start: it does nothing when the demo account already exists.
/// </summary>
public static class DemoDataSeeder
{
    public const string DemoEmail = "demo@applytrail.dev";
    public const string DemoPassword = "demo-password-123";

    private const int FollowUpDays = 7;

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        if (await db.Users.AnyAsync(u => u.Email == DemoEmail, ct))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var user = User.Create(DemoEmail, passwords.Hash(DemoPassword), now);
        db.Users.Add(user);
        db.Applications.AddRange(Build(user.Id, now));

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Exposed for tests: builds the demo applications without touching a database.</summary>
    public static IReadOnlyList<JobApplication> Build(Guid userId, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);

        DateTime DaysAgo(int days) => nowUtc.Date.AddDays(-days).AddHours(10);

        JobApplication Make(
            string company, string role, JobSource source, int appliedDaysAgo, string location, WorkMode mode,
            int? salaryMin, int? salaryMax, Sponsorship sponsorship, Priority priority, string? notes = null,
            params (ApplicationStatus Status, int DaysAgo)[] moves)
        {
            var applied = today.AddDays(-appliedDaysAgo);
            var application = JobApplication.Create(
                userId, company, role, ApplicationStatus.Applied, applied, applied, DaysAgo(appliedDaysAgo), FollowUpDays);

            application.Source = source;
            application.Location = location;
            application.WorkMode = mode;
            application.SalaryMin = salaryMin;
            application.SalaryMax = salaryMax;
            application.Sponsorship = sponsorship;
            application.Priority = priority;
            application.Notes = notes;

            foreach (var (status, daysAgo) in moves)
            {
                var at = DaysAgo(daysAgo);
                application.ChangeStatus(status, at, DateOnly.FromDateTime(at), FollowUpDays);
            }

            return application;
        }

        JobApplication Saved(
            string company, string role, JobSource source, string location, WorkMode mode, Priority priority, string? notes)
        {
            var application = JobApplication.Create(
                userId, company, role, ApplicationStatus.Saved, null, today, nowUtc, FollowUpDays);
            application.Source = source;
            application.Location = location;
            application.WorkMode = mode;
            application.Priority = priority;
            application.Notes = notes;
            return application;
        }

        // 1. In the interview stage, with a technical round coming up.
        var contoso = Make("Contoso Cloud", "Senior Software Engineer (.NET)", JobSource.LinkedIn, 21,
            "Dublin", WorkMode.Hybrid, 75_000, 90_000, Sponsorship.Offered, Priority.High,
            "Team builds an Azure-hosted booking platform. Mention the Angular migration project.",
            (ApplicationStatus.Screening, 14), (ApplicationStatus.Interviewing, 6));
        contoso.AddEvent(ActivityType.PhoneScreen, "Recruiter screen (30 min)", "Visa sponsorship confirmed on the call.",
            DaysAgo(13), nowUtc);
        contoso.AddEvent(ActivityType.Interview, "First technical interview", "System design and a C# exercise. Went well.",
            DaysAgo(5), nowUtc);
        contoso.AddReminder(ReminderType.Interview, "Pair-programming round", today.AddDays(2), new TimeOnly(14, 0));

        // 2. An offer on the table.
        var northwind = Make("Northwind Analytics", "Backend Engineer", JobSource.Indeed, 12,
            "Galway", WorkMode.Remote, 70_000, 80_000, Sponsorship.Offered, Priority.High,
            "Offer received. Asked for a week to decide.",
            (ApplicationStatus.Screening, 8), (ApplicationStatus.Interviewing, 5), (ApplicationStatus.Offer, 1));
        northwind.AddReminder(ReminderType.Deadline, "Reply to the offer", today.AddDays(5), null);

        // 3. Rejections and a ghosting, because a realistic job search has them.
        var fabrikam = Make("Fabrikam Health", "Full Stack Engineer", JobSource.CompanySite, 18,
            "Cork", WorkMode.Hybrid, 65_000, 78_000, Sponsorship.Unknown, Priority.Medium, null,
            (ApplicationStatus.Rejected, 9));
        var contosoRetail = Make("Contoso Retail", "Frontend Engineer (Angular)", JobSource.LinkedIn, 26,
            "Dublin", WorkMode.Onsite, 60_000, 72_000, Sponsorship.NotOffered, Priority.Low,
            "Role required no sponsorship.", (ApplicationStatus.Rejected, 20));
        var alpine = Make("Alpine Ski House", "Senior .NET Engineer", JobSource.LinkedIn, 30,
            "Dublin", WorkMode.Hybrid, 80_000, 95_000, Sponsorship.Offered, Priority.High,
            "Reached the final round. Ask for feedback.",
            (ApplicationStatus.Screening, 25), (ApplicationStatus.Interviewing, 20), (ApplicationStatus.Rejected, 12));
        var woodgrove = Make("Woodgrove Fintech", "Full Stack Developer", JobSource.Indeed, 9,
            "Dublin", WorkMode.Hybrid, 68_000, 76_000, Sponsorship.Unknown, Priority.Medium, null,
            (ApplicationStatus.Ghosted, 1));
        var lucerne = Make("Lucerne Publishing", "Integration Engineer", JobSource.JobBoard, 7,
            "Limerick", WorkMode.Onsite, 55_000, 65_000, Sponsorship.Unknown, Priority.Low,
            "Withdrew: needs relocation.", (ApplicationStatus.Withdrawn, 4));

        // 4. Going quiet: these should surface in "Going quiet" and the follow-up list.
        var tailspin = Make("Tailspin Systems", ".NET Developer", JobSource.LinkedIn, 15,
            "Dublin", WorkMode.Hybrid, 62_000, 74_000, Sponsorship.Unknown, Priority.Medium,
            "Applied via Easy Apply. No reply yet.");
        tailspin.AddReminder(ReminderType.Deadline, "Send updated CV to the recruiter", today.AddDays(-2), null);
        var wingtip = Make("Wingtip Mobility", "Software Engineer", JobSource.Recruiter, 14,
            "Remote (Ireland)", WorkMode.Remote, 70_000, 85_000, Sponsorship.Unknown, Priority.Medium,
            "Recruiter: Sam. Spoke once, then went quiet.", (ApplicationStatus.Screening, 12));

        // 5. Fresh applications, so this week's goal has progress.
        var adatum = Make("Adatum Payments", "Angular Developer", JobSource.JobBoard, 3,
            "Dublin", WorkMode.Hybrid, 64_000, 72_000, Sponsorship.Unknown, Priority.Medium);
        var litware = Make("Litware Labs", "Platform Engineer", JobSource.LinkedIn, 2,
            "Dublin", WorkMode.Hybrid, 72_000, 88_000, Sponsorship.Offered, Priority.High);
        var coho = Make("Coho Software", "Software Engineer II", JobSource.Referral, 5,
            "Athlone", WorkMode.Hybrid, 62_000, 70_000, Sponsorship.Unknown, Priority.High,
            "Referral from a former colleague. Send a thank-you either way.");
        var trey = Make("Trey Research", "Application Developer", JobSource.Indeed, 1,
            "Galway", WorkMode.Onsite, 58_000, 66_000, Sponsorship.Unknown, Priority.Low);

        // 6. Saved for later.
        var proseware = Saved("Proseware Digital", "Azure Cloud Engineer", JobSource.CompanySite,
            "Dublin", WorkMode.Hybrid, Priority.High, "Closes soon. Tailor the CV to Azure Functions work.");
        proseware.AddReminder(ReminderType.Deadline, "Application closes", today.AddDays(3), null);
        var blueYonder = Saved("Blue Yonder Airlines", "Software Developer", JobSource.Recruiter,
            "Dublin", WorkMode.Onsite, Priority.Medium, "Recruiter shared the spec. Decide this week.");

        return
        [
            contoso, northwind, fabrikam, contosoRetail, alpine, woodgrove, lucerne,
            tailspin, wingtip, adatum, litware, coho, trey, proseware, blueYonder,
        ];
    }
}
