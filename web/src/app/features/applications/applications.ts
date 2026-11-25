import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { DataStore } from '../../core/data-store';
import { todayLocal } from '../../core/dates';
import { errorMessage } from '../../core/api-error';
import { FormDialogService } from '../../core/form-dialog.service';
import {
  Application,
  ApplicationQuery,
  ApplicationStatus,
  PRIORITIES,
  PRIORITY_LABEL,
  Paged,
  SOURCES,
  SOURCE_LABEL,
  SPONSORSHIPS,
  SPONSORSHIP_LABEL,
  STATUSES,
} from '../../core/models';
import { STATUS_LABEL, statusClass } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon';
import { StripComponent } from '../../shared/strip';

const PAGE_SIZE = 25;

const SORTS = [
  { value: 'lastActivity:desc', label: 'Latest activity' },
  { value: 'appliedOn:desc', label: 'Applied, newest first' },
  { value: 'appliedOn:asc', label: 'Applied, oldest first' },
  { value: 'created:desc', label: 'Recently added' },
  { value: 'company:asc', label: 'Company A to Z' },
  { value: 'priority:desc', label: 'Priority, high first' },
] as const;

/** Filters live in the URL, so a filtered view can be bookmarked and the back button works. */
@Component({
  selector: 'at-applications',
  imports: [ReactiveFormsModule, IconComponent, StripComponent],
  templateUrl: './applications.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApplicationsComponent {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(DataStore);
  private readonly toast = inject(ToastService);
  protected readonly formDialog = inject(FormDialogService);
  private readonly params = toSignal(this.route.queryParamMap, { requireSync: true });

  protected readonly statuses = STATUSES;
  protected readonly sources = SOURCES;
  protected readonly priorities = PRIORITIES;
  protected readonly sponsorships = SPONSORSHIPS;
  protected readonly sorts = SORTS;
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly sourceLabel = SOURCE_LABEL;
  protected readonly priorityLabel = PRIORITY_LABEL;
  protected readonly sponsorshipLabel = SPONSORSHIP_LABEL;
  protected readonly statusClass = statusClass;
  protected readonly pageSize = PAGE_SIZE;

  protected readonly result = signal<Paged<Application> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly search = new FormControl('', { nonNullable: true });

  constructor() {
    // Keep the search box in step with the URL (e.g. after "Clear filters" or a link from another screen).
    effect(() => {
      const q = this.params().get('q') ?? '';
      if (q !== this.search.value) this.search.setValue(q, { emitEvent: false });
    });

    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((q) => this.update({ q: q.trim() || null, page: null }));

    effect(() => {
      this.store.version();
      const query = this.query();
      void this.load(query);
    });
  }

  protected query(): ApplicationQuery {
    const p = this.params();
    const [sort, dir] = (p.get('sort') ?? 'lastActivity:desc').split(':');
    const statusValues = p
      .getAll('status')
      .filter((s): s is ApplicationStatus => (STATUSES as readonly string[]).includes(s));
    return {
      q: p.get('q') ?? undefined,
      status: statusValues.length ? statusValues : undefined,
      source: (p.get('source') ?? '') as ApplicationQuery['source'],
      priority: (p.get('priority') ?? '') as ApplicationQuery['priority'],
      sponsorship: (p.get('sponsorship') ?? '') as ApplicationQuery['sponsorship'],
      archived: p.get('archived') === 'true' || undefined,
      sort: sort as ApplicationQuery['sort'],
      dir: dir as ApplicationQuery['dir'],
      page: Number(p.get('page')) || 1,
      pageSize: PAGE_SIZE,
    };
  }

  protected sortValue(): string {
    return this.params().get('sort') ?? 'lastActivity:desc';
  }

  protected hasFilters(): boolean {
    const p = this.params();
    return ['q', 'status', 'source', 'priority', 'sponsorship', 'archived'].some((k) => p.has(k));
  }

  protected isSelected(status: ApplicationStatus): boolean {
    return this.params().getAll('status').includes(status);
  }

  protected toggleStatus(status: ApplicationStatus): void {
    const current = this.params().getAll('status');
    const next = current.includes(status)
      ? current.filter((s) => s !== status)
      : [...current, status];
    this.update({ status: next.length ? next : null, page: null });
  }

  protected setFilter(key: 'source' | 'priority' | 'sponsorship' | 'sort', event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.update({ [key]: value || null, page: null });
  }

  protected setArchived(event: Event): void {
    this.update({
      archived: (event.target as HTMLInputElement).checked ? 'true' : null,
      page: null,
    });
  }

  protected goToPage(page: number): void {
    this.update({ page: page > 1 ? String(page) : null });
    window.scrollTo({ top: 0 });
  }

  protected clear(): void {
    this.search.setValue('', { emitEvent: false });
    void this.router.navigate([], { queryParams: {} });
  }

  protected retry(): void {
    this.store.invalidate();
  }

  protected pages(): number {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.total / r.pageSize)) : 1;
  }

  protected firstShown(): number {
    const r = this.result();
    return r && r.total > 0 ? (r.page - 1) * r.pageSize + 1 : 0;
  }

  protected lastShown(): number {
    const r = this.result();
    return r ? (r.page - 1) * r.pageSize + r.items.length : 0;
  }

  protected async changeStatus(app: Application, status: ApplicationStatus): Promise<void> {
    try {
      await this.api.changeStatus(app.id, status, todayLocal());
      this.toast.show(`${app.company} moved to ${STATUS_LABEL[status]}`);
      this.store.invalidate();
    } catch (error) {
      this.toast.error(errorMessage(error));
      this.store.invalidate(); // put the select back to what the server has
    }
  }

  private update(changes: Record<string, string | string[] | null>): void {
    void this.router.navigate([], {
      queryParams: changes,
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private async load(query: ApplicationQuery): Promise<void> {
    this.loading.set(true);
    try {
      this.result.set(await this.api.list(query));
      this.error.set(null);
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }
}
