namespace ApplyTrail.Domain;

/// <summary>
/// One job the user is pursuing. This aggregate owns the rules that make the tracker useful:
/// status changes are logged, finished applications stop nagging, and applying schedules a follow-up.
/// Every time-dependent method takes the clock values as parameters so the rules are easy to test.
/// </summary>
public class JobApplication
{
    private JobApplication() { } // EF Core

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    public string Company { get; set; } = "";
    public string RoleTitle { get; set; } = "";
    public string? JobUrl { get; set; }
    public JobSource Source { get; set; } = JobSource.Other;
    public string? Location { get; set; }
    public WorkMode WorkMode { get; set; } = WorkMode.Hybrid;
    public int? SalaryMin { get; set; }
    public int? SalaryMax { get; set; }
    public string Currency { get; set; } = "EUR";
    public Sponsorship Sponsorship { get; set; } = Sponsorship.Unknown;
    public Priority Priority { get; set; } = Priority.Medium;
    public string? Notes { get; set; }
    public DateOnly? AppliedOn { get; set; }

    public ApplicationStatus Status { get; private set; }

    /// <summary>Last time the employer (or the user) did something. Drives the "going quiet" list.</summary>
    public DateTime LastActivityAt { get; private set; }

    public DateTime? ArchivedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public List<ActivityEvent> Events { get; private set; } = [];
    public List<Reminder> Reminders { get; private set; } = [];

    public bool IsArchived => ArchivedAt is not null;

    /// <summary>
    /// Starts tracking a job. An application created as <see cref="ApplicationStatus.Applied"/>
    /// gets a follow-up reminder automatically.
    /// </summary>
    public static JobApplication Create(
        Guid userId,
        string company,
        string roleTitle,
        ApplicationStatus status,
        DateOnly? appliedOn,
        DateOnly today,
        DateTime nowUtc,
        int followUpAfterDays)
    {
        var application = new JobApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Company = company,
            RoleTitle = roleTitle,
            Status = status,
            AppliedOn = status == ApplicationStatus.Saved ? null : appliedOn ?? today,
            CreatedAt = nowUtc,
            LastActivityAt = nowUtc,
        };

        application.Events.Add(ActivityEvent.StatusChange(application.Id, null, status, nowUtc, note: null));

        if (status == ApplicationStatus.Applied)
        {
            application.EnsureFollowUp(today, followUpAfterDays);
        }

        return application;
    }

    /// <summary>
    /// Moves the application through the pipeline. Returns false if nothing changed.
    /// Side effects: logs a status-change event, stamps the applied date the first time the
    /// application leaves Saved, cancels the "chase them" follow-up once the employer has replied,
    /// cancels every open reminder when the application is over, and schedules a follow-up when it
    /// becomes Applied.
    /// </summary>
    public bool ChangeStatus(
        ApplicationStatus newStatus,
        DateTime nowUtc,
        DateOnly today,
        int followUpAfterDays,
        string? note = null)
    {
        if (newStatus == Status)
        {
            return false;
        }

        var previous = Status;
        Status = newStatus;
        LastActivityAt = nowUtc;
        Events.Add(ActivityEvent.StatusChange(Id, previous, newStatus, nowUtc, note));

        if (newStatus != ApplicationStatus.Saved)
        {
            AppliedOn ??= today;
        }

        if (newStatus.IsResponse())
        {
            // They answered, so chasing them is pointless. Interview and deadline reminders still apply.
            foreach (var reminder in Reminders.Where(r => r.Type == ReminderType.FollowUp))
            {
                reminder.Cancel(nowUtc);
            }
        }

        if (newStatus.IsTerminal())
        {
            foreach (var reminder in Reminders)
            {
                reminder.Cancel(nowUtc);
            }
        }

        if (newStatus == ApplicationStatus.Applied)
        {
            EnsureFollowUp(today, followUpAfterDays);
        }

        return true;
    }

    /// <summary>
    /// Adds something that already happened to the timeline. Notes never reset the
    /// "going quiet" clock; anything involving the employer does.
    /// </summary>
    public ActivityEvent AddEvent(
        ActivityType type, string title, string? details, DateTime occurredAtUtc, DateTime nowUtc)
    {
        if (type == ActivityType.StatusChange)
        {
            throw new ArgumentException("Status changes are recorded automatically.", nameof(type));
        }

        var entry = ActivityEvent.Create(Id, type, title, details, occurredAtUtc);
        Events.Add(entry);

        if (type != ActivityType.Note)
        {
            var effective = occurredAtUtc > nowUtc ? nowUtc : occurredAtUtc;
            if (effective > LastActivityAt)
            {
                LastActivityAt = effective;
            }
        }

        return entry;
    }

    public Reminder AddReminder(ReminderType type, string title, DateOnly dueOn, TimeOnly? timeOfDay)
    {
        var reminder = Reminder.Create(Id, type, title, dueOn, timeOfDay);
        Reminders.Add(reminder);
        return reminder;
    }

    public Reminder? FindReminder(Guid reminderId) => Reminders.FirstOrDefault(r => r.Id == reminderId);

    /// <summary>
    /// Marks a reminder done. Optionally logs it on the timeline and schedules the next nudge,
    /// which is how a chase becomes a rhythm instead of a one-off.
    /// </summary>
    public Reminder CompleteReminder(
        Guid reminderId,
        DateTime nowUtc,
        DateOnly today,
        bool logAsEvent,
        string? note,
        int? remindAgainInDays)
    {
        var reminder = FindReminder(reminderId)
            ?? throw new InvalidOperationException("Reminder does not belong to this application.");

        if (!reminder.IsOpen)
        {
            return reminder;
        }

        reminder.Complete(nowUtc);

        if (logAsEvent)
        {
            var (type, title) = reminder.Type switch
            {
                ReminderType.FollowUp => (ActivityType.Email, "Followed up"),
                ReminderType.Interview => (ActivityType.Interview, "Interview done"),
                _ => (ActivityType.Note, "Deadline handled"),
            };
            AddEvent(type, title, note, nowUtc, nowUtc);
        }

        if (remindAgainInDays is > 0 && !Status.IsTerminal())
        {
            AddReminder(reminder.Type, reminder.Title, today.AddDays(remindAgainInDays.Value), timeOfDay: null);
        }

        return reminder;
    }

    /// <summary>Pushes an open reminder to <paramref name="days"/> days from today.</summary>
    public Reminder SnoozeReminder(Guid reminderId, int days, DateOnly today)
    {
        var reminder = FindReminder(reminderId)
            ?? throw new InvalidOperationException("Reminder does not belong to this application.");

        if (!reminder.IsOpen)
        {
            throw new InvalidOperationException("Only open reminders can be snoozed.");
        }

        reminder.MoveTo(today.AddDays(days));
        return reminder;
    }

    public void Archive(DateTime nowUtc) => ArchivedAt ??= nowUtc;

    public void Unarchive() => ArchivedAt = null;

    /// <summary>
    /// An application is stale when it is still in play but nothing has happened for
    /// <paramref name="staleAfterDays"/> days or more.
    /// </summary>
    public bool IsStale(DateTime nowUtc, int staleAfterDays) =>
        !IsArchived
        && Status.IsActive()
        && nowUtc - LastActivityAt >= TimeSpan.FromDays(staleAfterDays);

    private void EnsureFollowUp(DateOnly today, int followUpAfterDays)
    {
        if (Reminders.Any(r => r.Type == ReminderType.FollowUp && r.IsOpen))
        {
            return;
        }

        // Never schedule something that is already overdue: bulk-entering last week's
        // applications should not flood the dashboard.
        var due = (AppliedOn ?? today).AddDays(followUpAfterDays);
        if (due < today)
        {
            due = today;
        }

        AddReminder(ReminderType.FollowUp, $"Follow up with {Company}", due, timeOfDay: null);
    }
}
