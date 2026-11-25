namespace ApplyTrail.Domain;

public enum ApplicationStatus { Saved, Applied, Screening, Interviewing, Offer, Rejected, Withdrawn, Ghosted }

public enum WorkMode { Onsite, Hybrid, Remote }

public enum Priority { Low, Medium, High }

public enum Sponsorship { Unknown, Offered, NotOffered }

public enum JobSource { LinkedIn, Indeed, CompanySite, Referral, Recruiter, JobBoard, Other }

public enum ActivityType { Note, Email, PhoneScreen, Interview, Assessment, Offer, StatusChange }

public enum ReminderType { FollowUp, Interview, Deadline }

public static class ApplicationStatusExtensions
{
    /// <summary>Still in play and worth chasing.</summary>
    public static bool IsActive(this ApplicationStatus status) =>
        status is ApplicationStatus.Applied or ApplicationStatus.Screening or ApplicationStatus.Interviewing;

    /// <summary>The application is over; open reminders no longer make sense.</summary>
    public static bool IsTerminal(this ApplicationStatus status) =>
        status is ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.Ghosted;

    /// <summary>The employer answered (a rejection counts as an answer).</summary>
    public static bool IsResponse(this ApplicationStatus status) =>
        status is ApplicationStatus.Screening or ApplicationStatus.Interviewing
            or ApplicationStatus.Offer or ApplicationStatus.Rejected;

    public static bool IsInterviewStage(this ApplicationStatus status) =>
        status is ApplicationStatus.Interviewing or ApplicationStatus.Offer;
}
