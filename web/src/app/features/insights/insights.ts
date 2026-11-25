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
import { formatDate, todayLocal } from '../../core/dates';
import { FormDialogService } from '../../core/form-dialog.service';
import { Insights, SOURCE_LABEL } from '../../core/models';
import { SettingsService } from '../../core/settings.service';
import { IconComponent } from '../../shared/icon';

const RANGES = [4, 8, 12, 26] as const;

/** Round a maximum up to a tidy axis top: 4, 5, 10, 20, 50... */
export function niceMax(value: number): number {
  if (value <= 4) return 4;
  const magnitude = 10 ** Math.floor(Math.log10(value));
  for (const step of [1, 2, 5, 10]) {
    if (value <= step * magnitude) return step * magnitude;
  }
  return 10 * magnitude;
}

export const percent = (fraction: number): string => `${Math.round(fraction * 100)}%`;

@Component({
  selector: 'at-insights',
  imports: [RouterLink, IconComponent],
  templateUrl: './insights.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InsightsComponent {
  private readonly api = inject(ApiService);
  private readonly store = inject(DataStore);
  private readonly settings = inject(SettingsService);
  protected readonly formDialog = inject(FormDialogService);

  protected readonly ranges = RANGES;
  protected readonly weeks = signal<number>(8);
  protected readonly data = signal<Insights | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly sourceLabel = SOURCE_LABEL;
  protected readonly percent = percent;
  protected readonly staleAfter = computed(() => this.settings.settings()?.staleAfterDays ?? 10);

  protected readonly axisMax = computed(() => {
    const weekly = this.data()?.weekly ?? [];
    return niceMax(Math.max(0, ...weekly.map((w) => Math.max(w.applied, w.responded))));
  });
  protected readonly ticks = computed(() => [this.axisMax(), this.axisMax() / 2, 0]);

  protected readonly funnel = computed(() => {
    const f = this.data()?.funnel;
    if (!f) return [];
    const rows = [
      { label: 'Applied', value: f.applied },
      { label: 'Heard back', value: f.responded },
      { label: 'Reached interviews', value: f.interviewed },
      { label: 'Offers', value: f.offers },
    ];
    return rows.map((r) => ({ ...r, share: f.applied ? r.value / f.applied : 0 }));
  });

  protected readonly smallSample = computed(() => {
    const applied = this.data()?.funnel.applied ?? 0;
    return applied > 0 && applied < 15;
  });

  /** Show every Nth week label so a 26-week chart stays legible. */
  protected readonly labelStep = computed(() =>
    this.weeks() <= 8 ? 1 : this.weeks() <= 14 ? 2 : 4,
  );

  constructor() {
    effect(() => {
      this.store.version();
      void this.load(this.weeks());
    });
  }

  protected setWeeks(event: Event): void {
    this.weeks.set(Number((event.target as HTMLSelectElement).value));
  }

  protected height(value: number): number {
    return Math.max(value > 0 ? 2 : 0, (value / this.axisMax()) * 100);
  }

  protected weekLabel(start: string): string {
    return formatDate(start, todayLocal());
  }

  protected weekSummary(w: { weekStart: string; applied: number; responded: number }): string {
    return `Week of ${this.weekLabel(w.weekStart)}: ${w.applied} applied, ${w.responded} first replies`;
  }

  protected averageText(days?: number | null): string {
    if (days == null) return 'No replies yet';
    return `${days} ${days === 1 ? 'day' : 'days'}`;
  }

  protected rate(part: number, whole: number): string {
    return whole ? ` (${percent(part / whole)})` : '';
  }

  private async load(weeks: number): Promise<void> {
    this.loading.set(true);
    try {
      this.data.set(await this.api.insights(weeks, todayLocal()));
      this.error.set(null);
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
}
