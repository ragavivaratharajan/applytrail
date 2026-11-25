import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiError, errorMessage } from '../../core/api-error';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { todayLocal } from '../../core/dates';
import { SettingsService } from '../../core/settings.service';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon';

export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

@Component({
  selector: 'at-settings',
  imports: [ReactiveFormsModule, IconComponent],
  templateUrl: './settings.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsComponent {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly settings = inject(SettingsService);
  private readonly deleteDialog = viewChild.required<ElementRef<HTMLDialogElement>>('deleteDialog');

  protected readonly saving = signal(false);
  protected readonly exporting = signal<'csv' | 'json' | null>(null);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly deleting = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    weeklyGoal: [10, [Validators.required, Validators.min(1), Validators.max(200)]],
    followUpAfterDays: [7, [Validators.required, Validators.min(1), Validators.max(60)]],
    staleAfterDays: [10, [Validators.required, Validators.min(1), Validators.max(120)]],
  });

  protected readonly deleteForm = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.maxLength(100)]],
  });

  constructor() {
    effect(() => {
      const s = this.settings.settings();
      if (s && !this.form.dirty) {
        this.form.reset({
          weeklyGoal: s.weeklyGoal,
          followUpAfterDays: s.followUpAfterDays,
          staleAfterDays: s.staleAfterDays,
        });
      }
    });
  }

  protected invalid(name: 'weeklyGoal' | 'followUpAfterDays' | 'staleAfterDays'): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.saving.set(true);
    try {
      await this.settings.save(this.form.getRawValue());
      this.form.markAsPristine();
      this.toast.show('Settings saved');
    } catch (error) {
      this.toast.error(errorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async export(kind: 'csv' | 'json'): Promise<void> {
    this.exporting.set(kind);
    try {
      const blob = kind === 'csv' ? await this.api.exportCsv() : await this.api.exportJson();
      downloadBlob(blob, `applytrail-${todayLocal()}.${kind}`);
    } catch (error) {
      this.toast.error(errorMessage(error));
    } finally {
      this.exporting.set(null);
    }
  }

  protected openDelete(): void {
    this.deleteForm.reset();
    this.deleteError.set(null);
    this.deleteDialog().nativeElement.showModal();
  }

  protected closeDelete(): void {
    this.deleteDialog().nativeElement.close();
  }

  protected async confirmDelete(): Promise<void> {
    this.deleteForm.markAllAsTouched();
    if (this.deleteForm.invalid) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    try {
      await this.api.deleteAccount(this.deleteForm.controls.password.value);
      this.closeDelete();
      this.auth.clear();
      this.settings.clear();
      this.toast.show('Your account and all its data were deleted');
      await this.router.navigate(['/login']);
    } catch (error) {
      this.deleteError.set(ApiError.from(error).messages.join(' '));
    } finally {
      this.deleting.set(false);
    }
  }
}
