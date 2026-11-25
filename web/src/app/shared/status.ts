import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ApplicationStatus, STATUSES } from '../core/models';
import { STATUS_HINT, STATUS_LABEL, statusClass } from '../core/status';

/** The status as a coloured pill. */
@Component({
  selector: 'at-status-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span [class]="'chip ' + cls()" [title]="hint()">
    <span class="size-2 rounded-full bg-[var(--c)]"></span>{{ label() }}
  </span>`,
})
export class StatusChipComponent {
  readonly status = input.required<ApplicationStatus>();
  protected cls = () => statusClass(this.status());
  protected label = () => STATUS_LABEL[this.status()];
  protected hint = () => STATUS_HINT[this.status()];
}

/**
 * A native select dressed as the status pill. Picking a new value announces it; the parent saves it.
 * Being a real select, it works with keyboard and touch without any drag-and-drop.
 */
@Component({
  selector: 'at-status-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <label [class]="chipClass()">
    <span class="size-2 rounded-full bg-[var(--c)]"></span>
    <span class="sr-only">Status</span>
    {{ label() }}
    <select
      class="absolute inset-0 size-full cursor-pointer opacity-0"
      [attr.aria-label]="'Status of ' + subject()"
      [value]="status()"
      (change)="pick($event)"
    >
      @for (s of statuses; track s) {
        <option [value]="s" [selected]="s === status()">{{ labels[s] }}</option>
      }
    </select>
    <svg
      class="pointer-events-none absolute right-1.5"
      width="10"
      height="10"
      viewBox="0 0 12 8"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
    >
      <path d="m1 1.5 5 5 5-5" />
    </svg>
  </label>`,
})
export class StatusSelectComponent {
  readonly status = input.required<ApplicationStatus>();
  /** Names the application for screen readers ("Status of Contoso, Backend Engineer"). */
  readonly subject = input('this application');
  /** Hide until focused: used on board cards, where the lane already shows the status. */
  readonly srOnly = input(false);
  readonly changed = output<ApplicationStatus>();

  protected readonly statuses = STATUSES;
  protected readonly labels = STATUS_LABEL;
  protected label = () => STATUS_LABEL[this.status()];
  protected chipClass = () =>
    `chip cursor-pointer pr-6 hover:brightness-95 ${statusClass(this.status())} ${this.srOnly() ? 'sr-only focus-within:not-sr-only' : 'relative'}`;

  protected pick(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as ApplicationStatus;
    if (value !== this.status()) this.changed.emit(value);
  }
}
