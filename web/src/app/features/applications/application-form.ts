import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';
import { ApiError } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { DataStore } from '../../core/data-store';
import { formatDate, todayLocal } from '../../core/dates';
import { FormDialogService, FormTarget } from '../../core/form-dialog.service';
import { tidyJobUrl } from '../../core/job-url';
import {
  ApplicationFields,
  ApplicationStatus,
  DuplicateMatch,
  JobSource,
  PRIORITIES,
  PRIORITY_LABEL,
  Priority,
  SOURCES,
  SOURCE_LABEL,
  SPONSORSHIPS,
  SPONSORSHIP_LABEL,
  STATUSES,
  Sponsorship,
  WORK_MODES,
  WORK_MODE_LABEL,
  WorkMode,
} from '../../core/models';
import { STATUS_LABEL } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon';

const LAST_SOURCE_KEY = 'applytrail.lastSource';

const remembered = (): JobSource | null => {
  try {
    const value = localStorage.getItem(LAST_SOURCE_KEY);
    return SOURCES.find((s) => s === value) ?? null;
  } catch {
    return null; // storage can be blocked; the form works without it
  }
};

const remember = (source: JobSource): void => {
  try {
    localStorage.setItem(LAST_SOURCE_KEY, source);
  } catch {
    /* ignore */
  }
};

function httpUrl(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value.trim();
  if (!value) return null;
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:' ? null : { url: true };
  } catch {
    return { url: true };
  }
}

function salaryRange(group: AbstractControl): ValidationErrors | null {
  const min = group.get('salaryMin')?.value as number | null;
  const max = group.get('salaryMax')?.value as number | null;
  return min != null && max != null && max < min ? { salaryRange: true } : null;
}

/** The add / edit dialog. It lives in the shell; any screen opens it through FormDialogService. */
@Component({
  selector: 'at-application-form',
  imports: [ReactiveFormsModule, RouterLink, IconComponent],
  templateUrl: './application-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApplicationFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly store = inject(DataStore);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly dialogService = inject(FormDialogService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly statuses = STATUSES;
  protected readonly sources = SOURCES;
  protected readonly workModes = WORK_MODES;
  protected readonly priorities = PRIORITIES;
  protected readonly sponsorships = SPONSORSHIPS;
  protected readonly statusLabel = STATUS_LABEL;
  protected readonly sourceLabel = SOURCE_LABEL;
  protected readonly workModeLabel = WORK_MODE_LABEL;
  protected readonly priorityLabel = PRIORITY_LABEL;
  protected readonly sponsorshipLabel = SPONSORSHIP_LABEL;

  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly duplicates = signal<DuplicateMatch[]>([]);
  protected readonly isEdit = signal(false);
  protected readonly today = todayLocal();

  protected readonly form = this.fb.nonNullable.group(
    {
      company: ['', [Validators.required, Validators.maxLength(150)]],
      roleTitle: ['', [Validators.required, Validators.maxLength(150)]],
      jobUrl: ['', [Validators.maxLength(2000), httpUrl]],
      status: ['Applied' as ApplicationStatus],
      appliedOn: [''],
      source: ['Other' as JobSource],
      sponsorship: ['Unknown' as Sponsorship],
      location: ['', Validators.maxLength(150)],
      workMode: ['Hybrid' as WorkMode],
      salaryMin: this.fb.control<number | null>(null, [
        Validators.min(0),
        Validators.max(10_000_000),
      ]),
      salaryMax: this.fb.control<number | null>(null, [
        Validators.min(0),
        Validators.max(10_000_000),
      ]),
      currency: ['EUR', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
      priority: ['Medium' as Priority],
      notes: ['', Validators.maxLength(5000)],
    },
    { validators: salaryRange },
  );

  private editingId: string | null = null;
  private duplicateRun = 0;

  constructor() {
    effect(() => {
      const target = this.dialogService.target();
      const el = this.dialog().nativeElement;
      if (target && !el.open) {
        this.prepare(target);
        el.showModal();
      } else if (!target && el.open) {
        el.close();
      }
    });

    // Editing after a failed save clears the old error message.
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.errors().length) this.errors.set([]);
    });

    this.form.valueChanges
      .pipe(
        map(() => {
          const v = this.form.getRawValue();
          return `${v.company.trim()}\u0000${v.roleTitle.trim()}\u0000${v.jobUrl.trim()}`;
        }),
        distinctUntilChanged(),
        debounceTime(450),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => void this.checkDuplicates());
  }

  protected showsAppliedDate(): boolean {
    return this.form.controls.status.value !== 'Saved';
  }

  /** Shortens a pasted link (LinkedIn search links especially) to the job itself, once you leave the field. */
  protected tidyJobLink(): void {
    const control = this.form.controls.jobUrl;
    const tidy = tidyJobUrl(control.value);
    if (tidy !== control.value) control.setValue(tidy);
  }

  protected invalid(name: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || control.dirty);
  }

  protected backdropClick(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) this.dialogService.close();
  }

  protected closed(): void {
    // Fires for Esc and for close(); either way the service should agree the dialog is shut.
    if (this.dialogService.target()) this.dialogService.close();
  }

  protected async submit(addAnother = false): Promise<void> {
    this.errors.set([]);
    this.tidyJobLink(); // Enter can submit without the field ever losing focus
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.errors.set(['Fix the highlighted fields and try again.']);
      return;
    }

    const raw = this.form.getRawValue();
    const fields: ApplicationFields = {
      company: raw.company.trim(),
      roleTitle: raw.roleTitle.trim(),
      jobUrl: raw.jobUrl.trim() || null,
      source: raw.source,
      location: raw.location.trim() || null,
      workMode: raw.workMode,
      salaryMin: raw.salaryMin,
      salaryMax: raw.salaryMax,
      currency: raw.currency.trim().toUpperCase(),
      sponsorship: raw.sponsorship,
      priority: raw.priority,
      notes: raw.notes.trim() || null,
      appliedOn: raw.status === 'Saved' ? null : raw.appliedOn || null,
    };

    this.saving.set(true);
    try {
      if (this.editingId) {
        await this.api.update(this.editingId, fields);
        this.toast.show('Changes saved');
      } else {
        await this.api.create({
          ...fields,
          status: raw.status,
          today: this.today,
          appliedOn: fields.appliedOn ?? (raw.status === 'Saved' ? null : this.today),
        });
        this.toast.show(`Added ${fields.company}`);
        remember(raw.source);
      }
      this.store.invalidate();

      if (addAnother && !this.editingId) {
        this.prepare({ mode: 'create' }, raw.status, raw.source);
        this.focusFirst();
      } else {
        this.dialogService.close();
      }
    } catch (error) {
      this.errors.set(ApiError.from(error).messages);
    } finally {
      this.saving.set(false);
    }
  }

  protected duplicateReason(match: DuplicateMatch): string {
    return match.reason === 'SameLink' ? 'Same job link' : 'Same company, similar role';
  }

  protected duplicateWhen(match: DuplicateMatch): string {
    const state = match.isArchived ? 'archived' : this.statusLabel[match.status].toLowerCase();
    return match.appliedOn ? `${state}, applied ${formatDate(match.appliedOn, this.today)}` : state;
  }

  private prepare(
    target: FormTarget,
    status: ApplicationStatus = 'Applied',
    source: JobSource = remembered() ?? 'Other',
  ): void {
    this.errors.set([]);
    this.duplicates.set([]);
    this.saving.set(false);
    this.form.reset();
    this.isEdit.set(target.mode === 'edit');

    if (target.mode === 'edit') {
      const a = target.application;
      this.editingId = a.id;
      this.form.reset({
        company: a.company,
        roleTitle: a.roleTitle,
        jobUrl: a.jobUrl ?? '',
        status: a.status,
        appliedOn: a.appliedOn ?? this.today,
        source: a.source,
        sponsorship: a.sponsorship,
        location: a.location ?? '',
        workMode: a.workMode,
        salaryMin: a.salaryMin ?? null,
        salaryMax: a.salaryMax ?? null,
        currency: a.currency,
        priority: a.priority,
        notes: a.notes ?? '',
      });
    } else {
      this.editingId = null;
      this.form.reset({ status, appliedOn: this.today, source, currency: 'EUR' });
    }
    this.focusFirst();
  }

  private focusFirst(): void {
    queueMicrotask(() =>
      this.dialog().nativeElement.querySelector<HTMLInputElement>('#f-company')?.focus(),
    );
  }

  private async checkDuplicates(): Promise<void> {
    const v = this.form.getRawValue();
    const company = v.company.trim();
    if (!this.dialogService.target() || !company || (!v.roleTitle.trim() && !v.jobUrl.trim())) {
      this.duplicates.set([]);
      return;
    }
    const run = ++this.duplicateRun;
    try {
      const matches = await this.api.checkDuplicates({
        company,
        roleTitle: v.roleTitle.trim() || undefined,
        jobUrl: v.jobUrl.trim() || undefined,
        excludeId: this.editingId ?? undefined,
      });
      if (run === this.duplicateRun) this.duplicates.set(matches);
    } catch {
      if (run === this.duplicateRun) this.duplicates.set([]); // a failed check should never block saving
    }
  }
}
