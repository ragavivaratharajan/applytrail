/** Types mirror the API's DTOs. Enums travel as their names; dates as "yyyy-MM-dd"; timestamps as UTC ISO strings. */

export const STATUSES = [
  'Saved',
  'Applied',
  'Screening',
  'Interviewing',
  'Offer',
  'Rejected',
  'Withdrawn',
  'Ghosted',
] as const;
export type ApplicationStatus = (typeof STATUSES)[number];

export const SOURCES = [
  'LinkedIn',
  'Indeed',
  'CompanySite',
  'Referral',
  'Recruiter',
  'JobBoard',
  'Other',
] as const;
export type JobSource = (typeof SOURCES)[number];

export const WORK_MODES = ['Onsite', 'Hybrid', 'Remote'] as const;
export type WorkMode = (typeof WORK_MODES)[number];

export const PRIORITIES = ['Low', 'Medium', 'High'] as const;
export type Priority = (typeof PRIORITIES)[number];

export const SPONSORSHIPS = ['Unknown', 'Offered', 'NotOffered'] as const;
export type Sponsorship = (typeof SPONSORSHIPS)[number];

/** Events a person can log by hand. Status changes are recorded automatically. */
export const MANUAL_ACTIVITIES = [
  'Note',
  'Email',
  'PhoneScreen',
  'Interview',
  'Assessment',
  'Offer',
] as const;
export type ManualActivity = (typeof MANUAL_ACTIVITIES)[number];
export type ActivityType = ManualActivity | 'StatusChange';

export const REMINDER_TYPES = ['FollowUp', 'Interview', 'Deadline'] as const;
export type ReminderType = (typeof REMINDER_TYPES)[number];

export type ReminderBucket = 'All' | 'Overdue' | 'Today' | 'Upcoming';
export type DuplicateReason = 'None' | 'SameLink' | 'SameCompanyAndRole';

export const SOURCE_LABEL: Record<JobSource, string> = {
  LinkedIn: 'LinkedIn',
  Indeed: 'Indeed',
  CompanySite: 'Company site',
  Referral: 'Referral',
  Recruiter: 'Recruiter',
  JobBoard: 'Job board',
  Other: 'Other',
};
export const WORK_MODE_LABEL: Record<WorkMode, string> = {
  Onsite: 'On-site',
  Hybrid: 'Hybrid',
  Remote: 'Remote',
};
export const PRIORITY_LABEL: Record<Priority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
};
export const SPONSORSHIP_LABEL: Record<Sponsorship, string> = {
  Unknown: 'Not sure',
  Offered: 'Offers sponsorship',
  NotOffered: 'No sponsorship',
};
export const ACTIVITY_LABEL: Record<ActivityType, string> = {
  Note: 'Note',
  Email: 'Email',
  PhoneScreen: 'Phone screen',
  Interview: 'Interview',
  Assessment: 'Assessment',
  Offer: 'Offer',
  StatusChange: 'Status change',
};
export const REMINDER_TYPE_LABEL: Record<ReminderType, string> = {
  FollowUp: 'Follow up',
  Interview: 'Interview',
  Deadline: 'Deadline',
};

export interface Application {
  id: string;
  company: string;
  roleTitle: string;
  jobUrl?: string | null;
  source: JobSource;
  location?: string | null;
  workMode: WorkMode;
  salaryMin?: number | null;
  salaryMax?: number | null;
  currency: string;
  sponsorship: Sponsorship;
  priority: Priority;
  status: ApplicationStatus;
  appliedOn?: string | null;
  lastActivityAt: string;
  createdAt: string;
  isArchived: boolean;
  isStale: boolean;
  nextReminderOn?: string | null;
  notes?: string | null;
}

export interface ActivityEvent {
  id: string;
  type: ActivityType;
  occurredAt: string;
  title: string;
  details?: string | null;
  fromStatus?: ApplicationStatus | null;
  toStatus?: ApplicationStatus | null;
}

export interface Reminder {
  id: string;
  applicationId: string;
  company: string;
  roleTitle: string;
  type: ReminderType;
  title: string;
  dueOn: string;
  timeOfDay?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;
}

export interface ApplicationDetail {
  application: Application;
  events: ActivityEvent[];
  reminders: Reminder[];
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ApplicationFields {
  company: string;
  roleTitle: string;
  jobUrl?: string | null;
  source: JobSource;
  location?: string | null;
  workMode: WorkMode;
  salaryMin?: number | null;
  salaryMax?: number | null;
  currency: string;
  sponsorship: Sponsorship;
  priority: Priority;
  notes?: string | null;
  appliedOn?: string | null;
}

export interface CreateApplicationBody extends ApplicationFields {
  status: ApplicationStatus;
  today: string;
}

export interface ApplicationQuery {
  q?: string;
  status?: ApplicationStatus[];
  source?: JobSource | '';
  priority?: Priority | '';
  sponsorship?: Sponsorship | '';
  archived?: boolean;
  sort?: 'company' | 'appliedOn' | 'priority' | 'created' | 'lastActivity';
  dir?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export interface DuplicateMatch {
  id: string;
  company: string;
  roleTitle: string;
  status: ApplicationStatus;
  appliedOn?: string | null;
  isArchived: boolean;
  reason: DuplicateReason;
}

export interface StaleApplication {
  id: string;
  company: string;
  roleTitle: string;
  status: ApplicationStatus;
  daysSinceActivity: number;
}

export interface DashboardSummary {
  goal: { target: number; appliedThisWeek: number };
  overdue: Reminder[];
  today: Reminder[];
  upcoming: Reminder[];
  stale: StaleApplication[];
  statusCounts: Record<string, number>;
}

export interface Insights {
  funnel: { applied: number; responded: number; interviewed: number; offers: number };
  responseRate: number;
  interviewRate: number;
  averageDaysToResponse?: number | null;
  weekly: { weekStart: string; applied: number; responded: number }[];
  bySource: { source: JobSource; applied: number; responded: number; interviewed: number }[];
  staleCount: number;
}

export interface UserSettings {
  email: string;
  weeklyGoal: number;
  followUpAfterDays: number;
  staleAfterDays: number;
}

export interface UpdateSettingsBody {
  weeklyGoal: number;
  followUpAfterDays: number;
  staleAfterDays: number;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  email: string;
}
