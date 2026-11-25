import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError } from './api-error';
import {
  ActivityEvent,
  Application,
  ApplicationDetail,
  ApplicationFields,
  ApplicationQuery,
  ApplicationStatus,
  CreateApplicationBody,
  DashboardSummary,
  DuplicateMatch,
  Insights,
  ManualActivity,
  Paged,
  Reminder,
  ReminderBucket,
  ReminderType,
  UpdateSettingsBody,
  UserSettings,
} from './models';

/** One method per endpoint. Every call rejects with an ApiError, so screens only ever catch one type. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  // ---- applications ----
  list(query: ApplicationQuery): Promise<Paged<Application>> {
    return this.call(
      this.http.get<Paged<Application>>('/api/applications', { params: toParams(query) }),
    );
  }

  get(id: string): Promise<ApplicationDetail> {
    return this.call(this.http.get<ApplicationDetail>(`/api/applications/${id}`));
  }

  create(body: CreateApplicationBody): Promise<Application> {
    return this.call(this.http.post<Application>('/api/applications', body));
  }

  update(id: string, body: ApplicationFields): Promise<Application> {
    return this.call(this.http.put<Application>(`/api/applications/${id}`, body));
  }

  changeStatus(
    id: string,
    status: ApplicationStatus,
    today: string,
    note?: string,
  ): Promise<Application> {
    return this.call(
      this.http.post<Application>(`/api/applications/${id}/status`, { status, today, note }),
    );
  }

  archive(id: string): Promise<Application> {
    return this.call(this.http.post<Application>(`/api/applications/${id}/archive`, {}));
  }

  unarchive(id: string): Promise<Application> {
    return this.call(this.http.post<Application>(`/api/applications/${id}/unarchive`, {}));
  }

  remove(id: string): Promise<void> {
    return this.call(this.http.delete<void>(`/api/applications/${id}`));
  }

  addEvent(
    id: string,
    body: { type: ManualActivity; title: string; details?: string },
  ): Promise<ActivityEvent> {
    return this.call(this.http.post<ActivityEvent>(`/api/applications/${id}/events`, body));
  }

  checkDuplicates(body: {
    company: string;
    roleTitle?: string;
    jobUrl?: string;
    excludeId?: string;
  }): Promise<DuplicateMatch[]> {
    return this.call(this.http.post<DuplicateMatch[]>('/api/applications/check-duplicates', body));
  }

  // ---- reminders ----
  reminders(bucket: ReminderBucket, today: string): Promise<Reminder[]> {
    return this.call(this.http.get<Reminder[]>('/api/reminders', { params: { bucket, today } }));
  }

  createReminder(
    applicationId: string,
    body: { type: ReminderType; title: string; dueOn: string },
  ): Promise<Reminder> {
    return this.call(
      this.http.post<Reminder>(`/api/applications/${applicationId}/reminders`, body),
    );
  }

  completeReminder(
    applicationId: string,
    reminderId: string,
    body: { logAsEvent: boolean; note?: string; remindAgainInDays?: number; today: string },
  ): Promise<Reminder> {
    return this.call(
      this.http.post<Reminder>(
        `/api/applications/${applicationId}/reminders/${reminderId}/complete`,
        body,
      ),
    );
  }

  snoozeReminder(
    applicationId: string,
    reminderId: string,
    days: number,
    today: string,
  ): Promise<Reminder> {
    return this.call(
      this.http.post<Reminder>(
        `/api/applications/${applicationId}/reminders/${reminderId}/snooze`,
        { days, today },
      ),
    );
  }

  // ---- dashboard ----
  dashboard(today: string): Promise<DashboardSummary> {
    return this.call(this.http.get<DashboardSummary>('/api/dashboard', { params: { today } }));
  }

  insights(weeks: number, today: string): Promise<Insights> {
    return this.call(this.http.get<Insights>('/api/insights', { params: { weeks, today } }));
  }

  // ---- account ----
  me(): Promise<UserSettings> {
    return this.call(this.http.get<UserSettings>('/api/me'));
  }

  updateSettings(body: UpdateSettingsBody): Promise<UserSettings> {
    return this.call(this.http.put<UserSettings>('/api/me/settings', body));
  }

  deleteAccount(password: string): Promise<void> {
    return this.call(this.http.delete<void>('/api/me', { body: { password } }));
  }

  // ---- export ----
  exportCsv(): Promise<Blob> {
    return this.call(this.http.get('/api/export/csv', { responseType: 'blob' }));
  }

  exportJson(): Promise<Blob> {
    return this.call(this.http.get('/api/export/json', { responseType: 'blob' }));
  }

  private async call<T>(request: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(request);
    } catch (error) {
      throw ApiError.from(error);
    }
  }
}

/** Arrays repeat the key (status=Applied&status=Screening), which is how ASP.NET binds them. Empty values are left out. */
export function toParams(query: ApplicationQuery): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) {
      for (const item of value) params = params.append(key, String(item));
    } else {
      params = params.set(key, String(value));
    }
  }
  return params;
}
