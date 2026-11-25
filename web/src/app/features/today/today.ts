import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { errorMessage } from '../../core/api-error';
import { DataStore } from '../../core/data-store';
import { formatLongDate, todayLocal } from '../../core/dates';
import { FormDialogService } from '../../core/form-dialog.service';
import { DashboardSummary, STATUSES, StaleApplication } from '../../core/models';
import { SettingsService } from '../../core/settings.service';
import { STATUS_LABEL, statusClass } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon';
import { ReminderDone, ReminderRowComponent, ReminderSnooze } from '../../shared/reminder-row';
import { StatusChipComponent } from '../../shared/status';

/** The first screen: what needs doing today, what is going quiet, and how the week is going. */
@Component({
  selector: 'at-today',
  imports: [RouterLink, IconComponent, ReminderRowComponent, StatusChipComponent],
  templateUrl: './today.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TodayComponent {
  private readonly api = inject(ApiService);
  private readonly store = inject(DataStore);
  private readonly toast = inject(ToastService);
  private readonly settings = inject(SettingsService);
  protected readonly formDialog = inject(FormDialogService);

  protected readonly today = signal(todayLocal());
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly statuses = STATUSES;
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly statusClass = statusClass;

  protected readonly heading = computed(() => formatLongDate(this.today()));
  protected readonly chaseDays = computed(() => this.settings.settings()?.followUpAfterDays ?? 7);

  protected readonly dueCount = computed(() => {
    const s = this.summary();
    return s ? s.overdue.length + s.today.length : 0;
  });

  protected readonly sentence = computed(() => {
    const s = this.summary();
    if (!s) return '';
    const due = this.dueCount();
    const quiet = s.stale.length;
    if (due === 0 && quiet === 0) return 'Nothing is waiting on you today.';
    const parts: string[] = [];
    if (due > 0) parts.push(`${due} ${due === 1 ? 'reminder is' : 'reminders are'} due`);
    if (quiet > 0)
      parts.push(`${quiet} ${quiet === 1 ? 'application has' : 'applications have'} gone quiet`);
    return parts.join(' and ') + '.';
  });

  protected readonly goalSegments = computed(() => {
    const goal = this.summary()?.goal;
    if (!goal) return [];
    const count = Math.min(goal.target, 20);
    const filled = Math.min(count, Math.round((goal.appliedThisWeek / goal.target) * count));
    return Array.from({ length: count }, (_, i) => i < filled);
  });

  protected readonly goalText = computed(() => {
    const goal = this.summary()?.goal;
    if (!goal) return '';
    if (goal.appliedThisWeek >= goal.target) {
      const extra = goal.appliedThisWeek - goal.target;
      return extra > 0 ? `Goal met, ${extra} extra` : 'Goal met';
    }
    return `${goal.target - goal.appliedThisWeek} to go`;
  });

  protected readonly pipeline = computed(() =>
    this.statuses.map((status) => ({ status, count: this.summary()?.statusCounts[status] ?? 0 })),
  );

  constructor() {
    effect(() => {
      this.store.version();
      void this.load();
    });
  }

  protected async done(event: ReminderDone): Promise<void> {
    const { reminder, chaseAgain } = event;
    await this.run(async () => {
      await this.api.completeReminder(reminder.applicationId, reminder.id, {
        logAsEvent: true,
        remindAgainInDays: chaseAgain ? this.chaseDays() : undefined,
        today: this.today(),
      });
      this.toast.show(
        chaseAgain
          ? `Logged. You will be reminded about ${reminder.company} in ${this.chaseDays()} days`
          : `Marked done: ${reminder.company}`,
      );
    });
  }

  protected async snooze(event: ReminderSnooze): Promise<void> {
    await this.run(async () => {
      await this.api.snoozeReminder(
        event.reminder.applicationId,
        event.reminder.id,
        event.days,
        this.today(),
      );
      this.toast.show(`Snoozed ${event.reminder.company} for ${event.days} days`);
    });
  }

  protected async markGhosted(app: StaleApplication): Promise<void> {
    await this.run(async () => {
      await this.api.changeStatus(app.id, 'Ghosted', this.today());
      this.toast.show(`${app.company} marked as no reply`);
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    try {
      await action();
    } catch (error) {
      this.toast.error(errorMessage(error));
    }
    this.store.invalidate();
  }

  private async load(): Promise<void> {
    this.today.set(todayLocal());
    try {
      this.summary.set(await this.api.dashboard(this.today()));
      this.error.set(null);
    } catch (error) {
      this.error.set(errorMessage(error));
    }
  }
}
