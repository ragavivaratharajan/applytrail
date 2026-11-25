import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { describeDue } from '../core/dates';
import { Reminder } from '../core/models';
import { IconComponent } from './icon';

export interface ReminderDone {
  reminder: Reminder;
  chaseAgain: boolean;
}
export interface ReminderSnooze {
  reminder: Reminder;
  days: number;
}

/** One reminder with the three things you actually do to it: mark done, chase again later, or push it back. */
@Component({
  selector: 'at-reminder-row',
  imports: [RouterLink, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block @container' },
  template: `
    <div [class]="rowClass()">
      <div class="min-w-0 flex-1">
        @if (!compact()) {
          <p class="flex flex-wrap items-baseline gap-x-2">
            <a
              class="font-display text-[1.05rem] font-semibold hover:underline"
              [routerLink]="['/applications', reminder().applicationId]"
              >{{ reminder().company }}</a
            >
            <span class="truncate text-soft">{{ reminder().roleTitle }}</span>
          </p>
        }
        <p class="mt-0.5 flex flex-wrap items-center gap-2 text-[0.9375rem]">
          <span [class]="dueClass()">{{ due().label }}</span>
          <span>{{ title() }}</span>
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <button
          type="button"
          class="btn btn-primary btn-sm"
          (click)="done.emit({ reminder: reminder(), chaseAgain: false })"
        >
          <at-icon name="check" [size]="15" />Done
        </button>
        @if (reminder().type === 'FollowUp') {
          <button
            type="button"
            class="btn btn-quiet btn-sm"
            (click)="done.emit({ reminder: reminder(), chaseAgain: true })"
          >
            Done, chase again in {{ chaseDays() }} days
          </button>
        }
        <button
          type="button"
          class="btn btn-text btn-sm"
          (click)="snooze.emit({ reminder: reminder(), days: 3 })"
        >
          Snooze 3 days
        </button>
      </div>
    </div>
  `,
})
export class ReminderRowComponent {
  readonly reminder = input.required<Reminder>();
  readonly today = input.required<string>();
  readonly chaseDays = input(7);
  /** Leave out the company and role, for use on that application's own page. */
  readonly compact = input(false);
  readonly done = output<ReminderDone>();
  readonly snooze = output<ReminderSnooze>();

  /** "Follow up with Contoso" next to the company name says the same thing twice, so drop the repeated part. */
  protected readonly title = computed(() => {
    const r = this.reminder();
    const suffix = ` with ${r.company}`;
    return r.title.endsWith(suffix) ? r.title.slice(0, -suffix.length) : r.title;
  });

  protected readonly due = computed(() => describeDue(this.reminder().dueOn, this.today()));

  protected readonly rowClass = computed(
    () =>
      'flex flex-col gap-3 rounded-md border border-line border-l-[5px] bg-surface p-3 @md:flex-row @md:items-center ' +
      (this.due().tone === 'overdue'
        ? 'border-l-danger'
        : this.due().tone === 'today'
          ? 'border-l-highlight'
          : 'border-l-line-strong'),
  );

  protected readonly dueClass = computed(() => {
    const tone = this.due().tone;
    return (
      'chip ' + (tone === 'overdue' ? 'chip-danger' : tone === 'today' ? 'chip-warn' : 'chip-plain')
    );
  });
}
