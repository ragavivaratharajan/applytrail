import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError } from './api-error';
import { AuthResponse } from './models';

/**
 * Holds the short-lived access token in memory only (never localStorage), so a script injected into the page
 * cannot read a long-lived credential. The refresh token lives in an HttpOnly cookie the browser sends by itself.
 *
 * These calls skip the auth interceptor (HttpBackend) so a failing refresh can never trigger another refresh.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly router = inject(Router);

  private readonly token = signal<string | null>(null);
  readonly email = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.token() !== null);

  private starting: Promise<void> | null = null;
  private refreshing: Promise<boolean> | null = null;

  accessToken(): string | null {
    return this.token();
  }

  /** Called once on startup: if the browser still has a valid refresh cookie, sign in silently. */
  start(): Promise<void> {
    this.starting ??= this.refresh().then(() => undefined);
    return this.starting;
  }

  async login(email: string, password: string): Promise<void> {
    await this.authenticate('/api/auth/login', { email, password });
  }

  async register(email: string, password: string): Promise<void> {
    await this.authenticate('/api/auth/register', { email, password });
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/api/auth/logout', {}));
    } catch {
      // Signing out locally is what matters; the server cookie expires on its own.
    }
    this.clear();
    await this.router.navigate(['/login']);
  }

  /** One refresh at a time: parallel requests that all hit 401 share the same attempt. */
  refresh(): Promise<boolean> {
    this.refreshing ??= this.doRefresh().finally(() => {
      this.refreshing = null;
    });
    return this.refreshing;
  }

  /** The session has ended (the refresh token was rejected): send the person to sign in, then back to where they were. */
  async goToLogin(): Promise<void> {
    const url = this.router.url;
    if (url.startsWith('/login') || url.startsWith('/register')) return;
    await this.router.navigate(['/login'], { queryParams: { returnUrl: url } });
  }

  /** After the account is deleted the cookie is already cleared by the server. */
  clear(): void {
    this.token.set(null);
    this.email.set(null);
  }

  private async doRefresh(): Promise<boolean> {
    try {
      const response = await firstValueFrom(this.http.post<AuthResponse>('/api/auth/refresh', {}));
      this.apply(response);
      return true;
    } catch (error) {
      // 401 means the session really ended; a network error should not sign anyone out.
      if (error instanceof HttpErrorResponse && error.status === 401) this.clear();
      return false;
    }
  }

  private async authenticate(
    url: string,
    body: { email: string; password: string },
  ): Promise<void> {
    try {
      this.apply(await firstValueFrom(this.http.post<AuthResponse>(url, body)));
    } catch (error) {
      throw ApiError.from(error);
    }
  }

  private apply(response: AuthResponse): void {
    this.token.set(response.accessToken);
    this.email.set(response.email);
  }
}
