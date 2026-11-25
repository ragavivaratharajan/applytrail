import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiError, errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { DataStore } from '../../core/data-store';
import {
  addDays,
  formatDate,
  formatDateTime,
  formatSalary,
  timeAgo,
  todayLocal,
} from '../../core/dates';
import { FormDialogService } from '../../core/form-dialog.service';
import {
  ACTIVITY_LABEL,
  ActivityEvent,
  Application,
  ApplicationDetail,
  ApplicationStatus,
  MANUAL_ACTIVITIES,
  ManualActivity,
  PRIORITY_LABEL,
  REMINDER_TYPES,
  REMINDER_TYPE_LABEL,
  ReminderType,
  SOURCE_LABEL,
  SPONSORSHIP_LABEL,
  WORK_MODE_LABEL,
} from '../../core/models';
import { SettingsService } from '../../core/settings.service';
import { STATUS_LABEL, statusClass } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog';
import { IconComponent } from '../../shared/icon';
import { ReminderDone, ReminderRowComponent, ReminderSnooze } from '../../shared/reminder-row';
import { StatusChipComponent, StatusSelectComponent } from '../../shared/status';

@Component({
  selector: 'at-application-detail',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    IconComponent,
    StatusChipComponent,
    StatusSelectComponent,
    ReminderRowComponent,
    ConfirmDialogComponent,
  ],
  templateUrl: './application-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApplicationDetailComponent {
  /** Bound from the route (:id) by withComponentInputBinding(). */
  readonly id = input.required<string>();

  private readonly api = inject(ApiService);
  private readonly store = inject(DataStore);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly settings = inject(SettingsService);
  protected readonly formDialog = inject(FormDialogService);
  private readonly confirm = viewChild.required(ConfirmDialogComponent);

  protected readonly detail = signal<ApplicationDetail | null>(null);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly today = signal(todayLocal());

  protected readonly activityTypes = MANUAL_ACTIVITIES;
  protected readonly reminderTypes = REMINDER_TYPES;
  protected readonly activityLabel = ACTIVITY_LABEL;
  protected readonly reminderLabel = REMINDER_TYPE_LABEL;
  protected readonly sourceLabel = SOURCE_LABEL;
  protected readonly workModeLabel = WORK_MODE_LABEL;
  protected readonly priorityLabel = PRIORITY_LABEL;
  protected readonly sponsorshipLabel = SPONSORSHIP_LABEL;
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly statusClass = statusClass;

  protected readonly chaseDays = computed(() => this.settings.settings()?.followUpAfterDays ?? 7);

  protected readonly app = computed(() => this.detail()?.application ?? null);

  protected readonly openReminders = computed(() =>
    (this.detail()?.reminders ?? [])
      .filter((r) => !r.completedAt && !r.cancelledAt)
      .sort((a, b) => a.dueOn.localeCompare(b.dueOn)),
  );

  protected readonly timeline = computed(() =>
    [...(this.detail()?.events ?? [])].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt)),
  );

  protected readonly facts = computed(() => {
    const a = this.app();
    if (!a) return [];
    const today = this.today();
    const rows: { label: string; value: string }[] = [
      { label: 'Applied', value: a.appliedOn ? formatDate(a.appliedOn, today) : 'Not yet' },
      { label: 'Source', value: SOURCE_LABEL[a.source] },
      { label: 'Location', value: a.location || 'Not set' },
      { label: 'Work mode', value: WORK_MODE_LABEL[a.workMode] },
      { label: 'Salary', value: formatSalary(a.salaryMin, a.salaryMax, a.currency) ?? 'Not set' },
      { label: 'Visa sponsorship', value: SPONSORSHIP_LABEL[a.sponsorship] },
      { label: 'Priority', value: PRIORITY_LABEL[a.priority] },
      { label: 'Last activity', value: timeAgo(a.lastActivityAt) },
    ];
    return rows;
  });

  protected readonly activityForm = this.fb.nonNullable.group({
    type: ['Note' as ManualActivity],
    title: ['', [Validators.required, Validators.maxLength(150)]],
    details: ['', Validators.maxLength(2000)],
  });
  protected readonly reminderForm = this.fb.nonNullable.group({
    type: ['FollowUp' as ReminderType],
    title: ['Follow up', [Validators.required, Validators.maxLength(150)]],
    dueOn: [addDays(todayLocal(), 7), Validators.required],
  });
  protected readonly formError = signal<string | null>(null);

  constructor() {
    effect(() => {
      this.store.version();
      void this.load(this.id());
    });
  }

  protected formatTime = formatDateTime;
  protected ago = (iso: string): string => timeAgo(iso);

  protected eventDot(event: ActivityEvent): string {
    return event.type === 'StatusChange' && event.toStatus
      ? statusClass(event.toStatus)
      : 'st-saved';
  }

  protected async changeStatus(status: ApplicationStatus): Promise<void> {
    const a = this.app();
    if (!a) return;
    await this.act(async () => {
      await this.api.changeStatus(a.id, status, this.today());
      this.toast.show(`Moved to ${STATUS_LABEL[status]}`);
    });
  }

  protected async toggleArchive(): Promise<void> {
    const a = this.app();
    if (!a) return;
    await this.act(async () => {
      await (a.isArchived ? this.api.unarchive(a.id) : this.api.archive(a.id));
      this.toast.show(
        a.isArchived ? 'Restored from the archive' : 'Archived. Find it under Archived in the list',
      );
    });
  }

  protected async remove(): Promise<void> {
    const a = this.app();
    if (!a || !(await this.confirm().open())) return;
    try {
      await this.api.remove(a.id);
      this.toast.show(`Deleted ${a.company}`);
      this.store.invalidate();
      await this.router.navigate(['/applications']);
    } catch (error) {
      this.toast.error(errorMessage(error));
    }
  }

  protected async done(event: ReminderDone): Promise<void> {
    const { reminder, chaseAgain } = event;
    await this.act(async () => {
      await this.api.completeReminder(reminder.applicationId, reminder.id, {
        logAsEvent: true,
        remindAgainInDays: chaseAgain ? this.chaseDays() : undefined,
        today: this.today(),
      });
      this.toast.show(
        chaseAgain ? `Logged. Next reminder in ${this.chaseDays()} days` : 'Marked done',
      );
    });
  }

  protected async snooze(event: ReminderSnooze): Promise<void> {
    await this.act(async () => {
      await this.api.snoozeReminder(
        event.reminder.applicationId,
        event.reminder.id,
        event.days,
        this.today(),
      );
      this.toast.show(`Snoozed for ${event.days} days`);
    });
  }

  protected async addEvent(): Promise<void> {
    const a = this.app();
    this.activityForm.markAllAsTouched();
    if (!a || this.activityForm.invalid) return;
    const { type, title, details } = this.activityForm.getRawValue();
    await this.act(async () => {
      await this.api.addEvent(a.id, {
        type,
        title: title.trim(),
        details: details.trim() || undefined,
      });
      this.activityForm.reset({ type, title: '', details: '' });
      this.toast.show('Added to the timeline');
    });
  }

  protected async addReminder(): Promise<void> {
    const a = this.app();
    this.reminderForm.markAllAsTouched();
    if (!a || this.reminderForm.invalid) return;
    const { type, title, dueOn } = this.reminderForm.getRawValue();
    await this.act(async () => {
      await this.api.createReminder(a.id, { type, title: title.trim(), dueOn });
      this.reminderForm.reset({
        type: 'FollowUp',
        title: 'Follow up',
        dueOn: addDays(this.today(), this.chaseDays()),
      });
      this.toast.show('Reminder added');
    });
  }

  protected invalid(form: 'activity' | 'reminder', name: string): boolean {
    const group: AbstractControl = form === 'activity' ? this.activityForm : this.reminderForm;
    const control = group.get(name);
    return !!control && control.invalid && control.touched;
  }

  protected openEdit(a: Application): void {
    this.formDialog.openEdit(a);
  }

  /** Runs a change, then reloads every screen. Failures become a message, never a half-updated page. */
  private async act(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.formError.set(null);
    try {
      await action();
      this.store.invalidate();
    } catch (error) {
      const message = ApiError.from(error).messages.join(' ');
      this.formError.set(message);
      this.toast.error(message);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(id: string): Promise<void> {
    this.today.set(todayLocal());
    try {
      this.detail.set(await this.api.get(id));
      this.notFound.set(false);
      this.error.set(null);
    } catch (error) {
      const e = ApiError.from(error);
      if (e.status === 404) {
        this.notFound.set(true);
      } else {
        this.error.set(e.messages.join(' '));
      }
    }
  }
}
