using ApplyTrail.Domain;

namespace ApplyTrail.UnitTests;

/// <summary>Shared fixtures. "Now" is a Friday; its week starts Monday 9 November 2026.</summary>
internal static class TestData
{
    public static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);
    public static readonly DateOnly Today = new(2026, 11, 13);
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static JobApplication App(
        string company = "Acme",
        string role = "Software Engineer",
        ApplicationStatus status = ApplicationStatus.Applied,
        DateOnly? appliedOn = null,
        DateTime? createdAt = null,
        int followUpDays = 7) =>
        JobApplication.Create(UserId, company, role, status, appliedOn, Today, createdAt ?? Now, followUpDays);

    /// <summary>
    /// Builds an application with a history: applied on <paramref name="appliedOn"/> (10:00 UTC),
    /// then each transition at its own timestamp.
    /// </summary>
    public static JobApplication WithHistory(
        string company,
        JobSource source,
        DateOnly appliedOn,
        params (ApplicationStatus Status, DateTime At)[] transitions)
    {
        var created = appliedOn.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc);
        var app = JobApplication.Create(
            UserId, company, "Software Engineer", ApplicationStatus.Applied, appliedOn, appliedOn, created, 7);
        app.Source = source;

        foreach (var (status, at) in transitions)
        {
            app.ChangeStatus(status, at, DateOnly.FromDateTime(at), followUpAfterDays: 7);
        }

        return app;
    }

    public static DateTime At(int month, int day, int hour = 10) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);
}
