import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  isDevMode,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError } from '../../core/api-error';
import { AuthService } from '../../core/auth.service';
import { addDays, todayLocal } from '../../core/dates';
import { Application } from '../../core/models';
import { BrandComponent } from '../../shared/brand';
import { StripComponent } from '../../shared/strip';
import { ToastHostComponent } from '../../shared/toast-host';

const sample = (
  over: Partial<Application> & Pick<Application, 'id' | 'company' | 'roleTitle' | 'status'>,
): Application => ({
  source: 'LinkedIn',
  workMode: 'Hybrid',
  currency: 'EUR',
  sponsorship: 'Unknown',
  priority: 'Medium',
  lastActivityAt: new Date().toISOString(),
  createdAt: new Date().toISOString(),
  isArchived: false,
  isStale: false,
  ...over,
});

/** Sign in and create account share one screen; the route says which. */
@Component({
  selector: 'at-auth',
  imports: [ReactiveFormsModule, RouterLink, BrandComponent, StripComponent, ToastHostComponent],
  templateUrl: './auth.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthComponent {
  /** 'login' or 'register', from the route's data. */
  readonly mode = input<'login' | 'register'>('login');

  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly showDemoHint = isDevMode();

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.maxLength(100)]],
  });

  /** A few invented strips so the sign-in screen shows what the app is about. */
  protected readonly samples: Application[] = [
    sample({
      id: 's1',
      company: 'Contoso',
      roleTitle: 'Senior .NET Developer',
      status: 'Interviewing',
      location: 'Dublin',
      appliedOn: addDays(todayLocal(), -9),
      sponsorship: 'Offered',
      priority: 'High',
      nextReminderOn: addDays(todayLocal(), 2),
    }),
    sample({
      id: 's3',
      company: 'Northwind',
      roleTitle: 'Angular Developer',
      status: 'Applied',
      location: 'Galway',
      appliedOn: addDays(todayLocal(), -14),
      isStale: true,
    }),
    sample({
      id: 's4',
      company: 'Adventure Works',
      roleTitle: 'Backend Engineer',
      status: 'Rejected',
      location: 'Cork',
      appliedOn: addDays(todayLocal(), -20),
    }),
  ];

  protected isRegister(): boolean {
    return this.mode() === 'register';
  }

  protected fillDemo(): void {
    this.form.setValue({ email: 'demo@applytrail.dev', password: 'demo-password-123' });
  }

  protected showError(name: 'email' | 'password'): boolean {
    const c = this.form.controls[name];
    return c.invalid && c.touched;
  }

  protected async submit(): Promise<void> {
    this.errors.set([]);
    this.form.markAllAsTouched();
    const { email, password } = this.form.getRawValue();

    if (this.isRegister() && password.length < 10) {
      this.errors.set(['Use a password of at least 10 characters.']);
      return;
    }
    if (this.form.invalid) return;

    this.busy.set(true);
    try {
      await (this.isRegister()
        ? this.auth.register(email, password)
        : this.auth.login(email, password));
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      // Only follow in-app paths, so a crafted link cannot send someone to another site after sign-in.
      await this.router.navigateByUrl(
        returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')
          ? returnUrl
          : '/today',
      );
    } catch (error) {
      this.errors.set(ApiError.from(error).messages);
    } finally {
      this.busy.set(false);
    }
  }
}
