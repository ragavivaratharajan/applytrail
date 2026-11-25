import { Injectable, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { UpdateSettingsBody, UserSettings } from './models';

/** Weekly goal and reminder timings. Loaded once after sign-in and shared by the screens that need them. */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly api = inject(ApiService);
  readonly settings = signal<UserSettings | null>(null);

  async load(): Promise<void> {
    this.settings.set(await this.api.me());
  }

  async save(body: UpdateSettingsBody): Promise<void> {
    this.settings.set(await this.api.updateSettings(body));
  }

  clear(): void {
    this.settings.set(null);
  }
}
