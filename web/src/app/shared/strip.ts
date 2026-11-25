import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { describeDue, formatDate, formatSalary, todayLocal } from '../core/dates';
import { Application, ApplicationStatus, SPONSORSHIP_LABEL, WORK_MODE_LABEL } from '../core/models';
import { PIPELINE, STATUS_LABEL, isClosed, stageIndex, statusClass } from '../core/status';
import { IconComponent } from './icon';
import { StatusSelectComponent } from './status';

/**
 * One application as a slim strip: a coloured tab for its status, the essentials, and a five-stop track that shows
 * how far it has got. It adapts to the width of its container, so the same strip works in a narrow board lane
 * and as a full-width row in the list.
 */
@Component({
  selector: 'at-strip',
  imports: [RouterLink, IconComponent, StatusSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block @container' },
  template: `
    <article [class]="stripClass()">
      <div class="flex flex-col gap-2.5 p-3 @3xl:flex-row @3xl:items-center @3xl:gap-5 @3xl:py-2.5">
        <div class="min-w-0 flex-1">
          <div class="flex items-start justify-between gap-2">
            <div
              role="heading"
              [attr.aria-level]="level()"
              class="strip-company font-display text-[1.05rem] leading-tight font-semibold"
            >
              <a
                [routerLink]="['/applications', app().id]"
                [class]="stretch() ? 'after:absolute after:inset-0' : ''"
                >{{ app().company }}</a
              >
            </div>
            @if (app().priority === 'High') {
              <span class="inline-flex text-[#b07d00] dark:text-highlight" title="High priority">
                <at-icon name="flag" [size]="16" /><span class="sr-only">High priority</span>
              </span>
            }
          </div>
          <p class="truncate text-soft">{{ app().roleTitle }}</p>
          @if (meta()) {
            <p class="truncate text-[0.8125rem] text-soft">{{ meta() }}</p>
          }
        </div>

        <div class="flex items-center gap-3 @3xl:w-56 @3xl:flex-none">
          <div
            class="track"
            role="img"
            [attr.aria-label]="trackLabel()"
            [class.opacity-60]="closed()"
          >
            @for (stop of pipeline; track stop; let i = $index) {
              @if (i > 0) {
                <span class="track-bar" [class.is-done]="i <= stage()"></span>
              }
              <span
                class="track-stop"
                [class.is-done]="i < stage()"
                [class.is-now]="i === stage()"
              ></span>
            }
            @if (closed()) {
              <span class="ml-2 inline-flex text-[var(--c)]"
                ><at-icon [name]="endIcon()" [size]="16"
              /></span>
            }
          </div>
          <span class="text-[0.8125rem] whitespace-nowrap text-soft">{{ dateText() }}</span>
        </div>

        <div
          class="relative z-10 flex flex-wrap items-center gap-1.5 @3xl:w-80 @3xl:flex-none @3xl:justify-end"
        >
          @if (app().sponsorship === 'Offered') {
            <span class="chip st-offer">{{ sponsorship.Offered }}</span>
          } @else if (app().sponsorship === 'NotOffered') {
            <span class="chip chip-plain">{{ sponsorship.NotOffered }}</span>
          }
          @if (app().isStale && !closed()) {
            <span class="chip chip-warn"><at-icon name="clock" [size]="13" />Gone quiet</span>
          }
          @if (due(); as d) {
            <span
              [class]="
                'chip ' +
                (d.tone === 'overdue'
                  ? 'chip-danger'
                  : d.tone === 'today'
                    ? 'chip-warn'
                    : 'chip-plain')
              "
            >
              Follow-up {{ d.label.toLowerCase() === 'today' ? 'today' : d.label }}
            </span>
          }
          <at-status-select
            [status]="app().status"
            [subject]="app().company + ', ' + app().roleTitle"
            [srOnly]="hideStatus()"
            (changed)="statusChange.emit($event)"
          />
        </div>
      </div>
    </article>
  `,
})
export class StripComponent {
  readonly app = input.required<Application>();
  /** Make the whole strip a link to the detail page (the list does this; the board keeps drag free of accidental clicks). */
  readonly stretch = input(false);
  /** Hide the status control until it is focused. The board's lanes already show the status. */
  readonly hideStatus = input(false);
  /** Heading level of the company name: 3 under a lane's h2 on the board, 2 under the page's h1 in the list. */
  readonly level = input(3);
  readonly statusChange = output<ApplicationStatus>();

  protected readonly pipeline = PIPELINE;
  protected readonly sponsorship = SPONSORSHIP_LABEL;
  private readonly today = todayLocal();

  protected readonly stage = computed(() => stageIndex(this.app().status));
  protected readonly closed = computed(() => isClosed(this.app().status));

  protected readonly stripClass = computed(() => {
    const a = this.app();
    return `strip ${statusClass(a.status)} ${a.status === 'Ghosted' ? 'is-ghosted' : ''} ${this.closed() ? 'is-closed' : ''}`;
  });

  protected readonly meta = computed(() => {
    const a = this.app();
    const salary = formatSalary(a.salaryMin, a.salaryMax, a.currency);
    return [a.location, WORK_MODE_LABEL[a.workMode], salary].filter(Boolean).join(' · ');
  });

  protected readonly dateText = computed(() => {
    const a = this.app();
    if (a.appliedOn) return `Applied ${formatDate(a.appliedOn, this.today)}`;
    return `Saved ${formatDate(a.createdAt.slice(0, 10), this.today)}`;
  });

  protected readonly due = computed(() => {
    const next = this.app().nextReminderOn;
    return next ? describeDue(next, this.today) : null;
  });

  protected readonly endIcon = computed(() => {
    const s = this.app().status;
    return s === 'Rejected' ? 'x' : s === 'Withdrawn' ? 'undo' : 'clock';
  });

  protected readonly trackLabel = computed(() => {
    const s = this.app().status;
    return this.closed()
      ? `Ended: ${STATUS_LABEL[s]}`
      : `Stage ${this.stage() + 1} of ${PIPELINE.length}: ${STATUS_LABEL[s]}`;
  });
}
