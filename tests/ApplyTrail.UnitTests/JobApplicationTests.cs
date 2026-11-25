using ApplyTrail.Domain;
using static ApplyTrail.UnitTests.TestData;

namespace ApplyTrail.UnitTests;

public class JobApplicationTests
{
    // ---- creation -------------------------------------------------------------------------

    [Fact]
    public void Creating_an_applied_application_schedules_a_follow_up()
    {
        var app = App();

        Assert.Equal(Today, app.AppliedOn);
        var reminder = Assert.Single(app.Reminders);
        Assert.Equal(ReminderType.FollowUp, reminder.Type);
        Assert.Equal(Today.AddDays(7), reminder.DueOn);
        Assert.Equal("Follow up with Acme", reminder.Title);

        var created = Assert.Single(app.Events);
        Assert.Equal(ActivityType.StatusChange, created.Type);
        Assert.Null(created.FromStatus);
        Assert.Equal(ApplicationStatus.Applied, created.ToStatus);
        Assert.Equal("Added as Applied", created.Title);
    }

    [Fact]
    public void Creating_a_saved_application_has_no_applied_date_and_no_reminder()
    {
        var app = App(status: ApplicationStatus.Saved);

        Assert.Null(app.AppliedOn);
        Assert.Empty(app.Reminders);
    }

    [Fact]
    public void Back_dated_applications_never_get_an_already_overdue_follow_up()
    {
        // Applied 20 days ago + 7 days would be due 13 days ago: that would flood the dashboard.
        var app = App(appliedOn: Today.AddDays(-20));

        Assert.Equal(Today, Assert.Single(app.Reminders).DueOn);
    }

    [Fact]
    public void Recently_back_dated_applications_keep_the_natural_follow_up_date()
    {
        var app = App(appliedOn: Today.AddDays(-3));

        Assert.Equal(Today.AddDays(4), Assert.Single(app.Reminders).DueOn);
    }

    // ---- status changes -------------------------------------------------------------------

    [Fact]
    public void Changing_to_the_same_status_does_nothing()
    {
        var app = App();

        var changed = app.ChangeStatus(ApplicationStatus.Applied, Now.AddDays(1), Today.AddDays(1), 7);

        Assert.False(changed);
        Assert.Single(app.Events);
        Assert.Equal(Now, app.LastActivityAt);
    }

    [Fact]
    public void Changing_status_logs_an_event_and_updates_last_activity()
    {
        var app = App();
        var later = Now.AddDays(4);

        var changed = app.ChangeStatus(ApplicationStatus.Screening, later, Today.AddDays(4), 7, note: "Recruiter call booked");

        Assert.True(changed);
        Assert.Equal(ApplicationStatus.Screening, app.Status);
        Assert.Equal(later, app.LastActivityAt);

        var last = app.Events[^1];
        Assert.Equal(ApplicationStatus.Applied, last.FromStatus);
        Assert.Equal(ApplicationStatus.Screening, last.ToStatus);
        Assert.Equal("Moved to Screening", last.Title);
        Assert.Equal("Recruiter call booked", last.Details);
    }

    [Fact]
    public void Leaving_saved_stamps_the_applied_date_and_schedules_a_follow_up()
    {
        var app = App(status: ApplicationStatus.Saved);

        app.ChangeStatus(ApplicationStatus.Applied, Now, Today, 7);

        Assert.Equal(Today, app.AppliedOn);
        Assert.Equal(Today.AddDays(7), Assert.Single(app.Reminders).DueOn);
    }

    [Fact]
    public void Moving_from_saved_straight_to_a_later_stage_still_stamps_the_applied_date()
    {
        var app = App(status: ApplicationStatus.Saved);

        app.ChangeStatus(ApplicationStatus.Screening, Now, Today, 7);

        Assert.Equal(Today, app.AppliedOn);
        Assert.Empty(app.Reminders); // a follow-up only makes sense while waiting after applying
    }

    [Fact]
    public void Finishing_an_application_cancels_its_open_reminders()
    {
        var app = App();

        app.ChangeStatus(ApplicationStatus.Rejected, Now.AddDays(2), Today.AddDays(2), 7);

        var reminder = Assert.Single(app.Reminders);
        Assert.False(reminder.IsOpen);
        Assert.Equal(Now.AddDays(2), reminder.CancelledAt);
        Assert.Null(reminder.CompletedAt);
    }

    [Theory]
    [InlineData(ApplicationStatus.Screening)]
    [InlineData(ApplicationStatus.Interviewing)]
    [InlineData(ApplicationStatus.Offer)]
    public void A_reply_makes_the_follow_up_obsolete_but_keeps_other_reminders(ApplicationStatus reply)
    {
        var app = App();
        var followUp = Assert.Single(app.Reminders);
        var interview = app.AddReminder(ReminderType.Interview, "Call with the team", Today.AddDays(2), new TimeOnly(11, 0));

        app.ChangeStatus(reply, Now.AddDays(1), Today.AddDays(1), 7);

        Assert.False(followUp.IsOpen);
        Assert.Equal(Now.AddDays(1), followUp.CancelledAt);
        Assert.True(interview.IsOpen);
    }

    [Fact]
    public void Going_back_to_applied_after_a_reply_schedules_a_new_follow_up()
    {
        var app = App();

        app.ChangeStatus(ApplicationStatus.Screening, Now.AddDays(1), Today.AddDays(1), 7);
        app.ChangeStatus(ApplicationStatus.Applied, Now.AddDays(2), Today.AddDays(2), 7);

        Assert.Equal(2, app.Reminders.Count);
        Assert.Single(app.Reminders, r => r.IsOpen);
    }

    [Fact]
    public void Re_applying_while_a_follow_up_is_still_open_does_not_duplicate_it()
    {
        var app = App();

        app.ChangeStatus(ApplicationStatus.Saved, Now.AddDays(1), Today.AddDays(1), 7); // not a reply, not the end
        app.ChangeStatus(ApplicationStatus.Applied, Now.AddDays(2), Today.AddDays(2), 7);

        Assert.Single(app.Reminders);
    }

    [Fact]
    public void Reopening_a_rejected_application_schedules_a_fresh_follow_up()
    {
        var app = App(appliedOn: Today.AddDays(-30));
        app.ChangeStatus(ApplicationStatus.Rejected, Now, Today, 7);

        app.ChangeStatus(ApplicationStatus.Applied, Now.AddHours(1), Today, 7);

        Assert.Equal(2, app.Reminders.Count);
        var open = Assert.Single(app.Reminders, r => r.IsOpen);
        Assert.Equal(Today, open.DueOn);
    }

    // ---- timeline -------------------------------------------------------------------------

    [Fact]
    public void Notes_do_not_reset_the_going_quiet_clock()
    {
        var app = App();

        app.AddEvent(ActivityType.Note, "Researched the team", null, Now.AddDays(3), Now.AddDays(3));

        Assert.Equal(Now, app.LastActivityAt);
    }

    [Fact]
    public void Employer_activity_resets_the_going_quiet_clock()
    {
        var app = App();

        app.AddEvent(ActivityType.Email, "Recruiter replied", null, Now.AddDays(3), Now.AddDays(3));

        Assert.Equal(Now.AddDays(3), app.LastActivityAt);
    }

    [Fact]
    public void Back_dated_employer_activity_never_moves_the_clock_backwards()
    {
        var app = App();
        app.AddEvent(ActivityType.Email, "Latest", null, Now.AddDays(5), Now.AddDays(5));

        app.AddEvent(ActivityType.PhoneScreen, "Earlier call, logged late", null, Now.AddDays(2), Now.AddDays(6));

        Assert.Equal(Now.AddDays(5), app.LastActivityAt);
    }

    [Fact]
    public void Future_dated_activity_is_capped_at_now()
    {
        var app = App();

        app.AddEvent(ActivityType.Interview, "Interview next week", null, Now.AddDays(7), Now.AddDays(1));

        Assert.Equal(Now.AddDays(1), app.LastActivityAt);
    }

    [Fact]
    public void Status_changes_cannot_be_logged_by_hand()
    {
        var app = App();

        Assert.Throws<ArgumentException>(
            () => app.AddEvent(ActivityType.StatusChange, "Sneaky", null, Now, Now));
    }

    // ---- reminders ------------------------------------------------------------------------

    [Fact]
    public void Completing_a_follow_up_logs_it_and_schedules_the_next_nudge()
    {
        var app = App();
        var followUp = Assert.Single(app.Reminders);
        var due = Now.AddDays(7);

        app.CompleteReminder(followUp.Id, due, Today.AddDays(7), logAsEvent: true, note: "Emailed Sam", remindAgainInDays: 5);

        Assert.Equal(due, followUp.CompletedAt);
        Assert.Equal(2, app.Reminders.Count);

        var next = Assert.Single(app.Reminders, r => r.IsOpen);
        Assert.Equal(ReminderType.FollowUp, next.Type);
        Assert.Equal(Today.AddDays(12), next.DueOn);

        var logged = app.Events[^1];
        Assert.Equal(ActivityType.Email, logged.Type);
        Assert.Equal("Followed up", logged.Title);
        Assert.Equal("Emailed Sam", logged.Details);
        Assert.Equal(due, app.LastActivityAt);
    }

    [Fact]
    public void Completing_without_logging_leaves_the_timeline_alone()
    {
        var app = App();
        var followUp = Assert.Single(app.Reminders);

        app.CompleteReminder(followUp.Id, Now, Today, logAsEvent: false, note: null, remindAgainInDays: null);

        Assert.Single(app.Events);
        Assert.Single(app.Reminders);
        Assert.False(followUp.IsOpen);
    }

    [Fact]
    public void Completing_an_interview_reminder_logs_an_interview()
    {
        var app = App();
        var interview = app.AddReminder(ReminderType.Interview, "Technical round", Today, new TimeOnly(14, 0));

        app.CompleteReminder(interview.Id, Now, Today, logAsEvent: true, note: null, remindAgainInDays: null);

        var logged = app.Events[^1];
        Assert.Equal(ActivityType.Interview, logged.Type);
        Assert.Equal("Interview done", logged.Title);
    }

    [Fact]
    public void Completing_a_reminder_twice_is_harmless()
    {
        var app = App();
        var followUp = Assert.Single(app.Reminders);
        app.CompleteReminder(followUp.Id, Now, Today, true, null, remindAgainInDays: 3);
        var events = app.Events.Count;
        var reminders = app.Reminders.Count;

        app.CompleteReminder(followUp.Id, Now.AddHours(1), Today, true, null, remindAgainInDays: 3);

        Assert.Equal(events, app.Events.Count);
        Assert.Equal(reminders, app.Reminders.Count);
    }

    [Fact]
    public void Finished_applications_are_not_nagged_again()
    {
        var app = App();
        app.ChangeStatus(ApplicationStatus.Rejected, Now, Today, 7);
        var manual = app.AddReminder(ReminderType.Deadline, "Send thank-you note", Today, null);

        app.CompleteReminder(manual.Id, Now, Today, true, null, remindAgainInDays: 5);

        Assert.DoesNotContain(app.Reminders, r => r.IsOpen);
    }

    [Fact]
    public void Completing_a_reminder_from_another_application_is_rejected()
    {
        var app = App();

        Assert.Throws<InvalidOperationException>(
            () => app.CompleteReminder(Guid.NewGuid(), Now, Today, true, null, null));
    }

    [Fact]
    public void Snoozing_counts_from_today_not_from_the_old_due_date()
    {
        var app = App(appliedOn: Today.AddDays(-3)); // follow-up due in 4 days
        var followUp = Assert.Single(app.Reminders);

        app.SnoozeReminder(followUp.Id, days: 3, Today);

        Assert.Equal(Today.AddDays(3), followUp.DueOn);
        Assert.True(followUp.IsOpen);
    }

    [Fact]
    public void Only_open_reminders_can_be_snoozed()
    {
        var app = App();
        var followUp = Assert.Single(app.Reminders);
        app.CompleteReminder(followUp.Id, Now, Today, false, null, null);

        Assert.Throws<InvalidOperationException>(() => app.SnoozeReminder(followUp.Id, 2, Today));
    }

    // ---- stale / archive ------------------------------------------------------------------

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(30, true)]
    public void An_active_application_goes_stale_after_the_configured_quiet_period(int quietDays, bool expected)
    {
        var app = App(createdAt: Now.AddDays(-quietDays));

        Assert.Equal(expected, app.IsStale(Now, staleAfterDays: 10));
    }

    [Theory]
    [InlineData(ApplicationStatus.Saved)]
    [InlineData(ApplicationStatus.Offer)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.Ghosted)]
    public void Only_applications_still_in_play_can_go_stale(ApplicationStatus status)
    {
        var old = Now.AddDays(-60);
        var app = App(status: ApplicationStatus.Applied, createdAt: old);
        app.ChangeStatus(status, old, Today.AddDays(-60), 7);

        Assert.False(app.IsStale(Now, staleAfterDays: 10));
    }

    [Fact]
    public void Archived_applications_are_never_stale()
    {
        var app = App(createdAt: Now.AddDays(-60));
        app.Archive(Now);

        Assert.False(app.IsStale(Now, 10));
    }

    [Fact]
    public void Archiving_twice_keeps_the_first_timestamp_and_unarchiving_restores()
    {
        var app = App();

        app.Archive(Now);
        app.Archive(Now.AddDays(5));
        Assert.Equal(Now, app.ArchivedAt);
        Assert.True(app.IsArchived);

        app.Unarchive();
        Assert.False(app.IsArchived);
        Assert.Null(app.ArchivedAt);
    }
}
