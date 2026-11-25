import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Wordmark: a dotted trail from where you started to where you are heading, ending in a highlighter dot. */
@Component({
  selector: 'at-brand',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="inline-flex items-center gap-2.5">
      <svg width="30" height="30" viewBox="0 0 32 32" aria-hidden="true">
        <rect width="32" height="32" rx="8" fill="var(--primary)" />
        <path
          d="M7 22c4-1 5-6 9-6s5-5 9-6"
          fill="none"
          stroke="var(--on-primary)"
          stroke-opacity=".6"
          stroke-width="2"
          stroke-linecap="round"
          stroke-dasharray="1 4"
        />
        <circle cx="7" cy="22" r="3" fill="var(--on-primary)" fill-opacity=".75" />
        <circle cx="25" cy="10" r="4" fill="var(--highlight)" />
      </svg>
      <span
        class="font-display text-[1.25rem] font-bold tracking-tight"
        [class.sr-only]="iconOnly()"
        >ApplyTrail</span
      >
    </span>
  `,
})
export class BrandComponent {
  readonly iconOnly = input(false);
}
