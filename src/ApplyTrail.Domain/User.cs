namespace ApplyTrail.Domain;

public class User
{
    private User() { } // EF Core

    public Guid Id { get; private set; }

    /// <summary>Unique, stored lower-case.</summary>
    public string Email { get; private set; } = "";

    public string PasswordHash { get; private set; } = "";
    public int WeeklyGoal { get; private set; } = 10;
    public int FollowUpAfterDays { get; private set; } = 7;
    public int StaleAfterDays { get; private set; } = 10;
    public DateTime CreatedAt { get; private set; }

    public static User Create(string email, string passwordHash, DateTime nowUtc) => new()
    {
        Id = Guid.NewGuid(),
        Email = email.Trim().ToLowerInvariant(),
        PasswordHash = passwordHash,
        CreatedAt = nowUtc,
    };

    public void UpdateSettings(int weeklyGoal, int followUpAfterDays, int staleAfterDays)
    {
        WeeklyGoal = weeklyGoal;
        FollowUpAfterDays = followUpAfterDays;
        StaleAfterDays = staleAfterDays;
    }
}

/// <summary>The per-user knobs that drive reminders and the dashboard.</summary>
public readonly record struct UserSettings(int WeeklyGoal, int FollowUpAfterDays, int StaleAfterDays)
{
    public static UserSettings Default { get; } = new(10, 7, 10);
}
