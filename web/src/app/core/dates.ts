const LOCALE = 'en-IE';

const pad = (n: number): string => String(n).padStart(2, '0');

/** The person's calendar date (not UTC), as "yyyy-MM-dd". The API needs it to decide what is overdue today. */
export function todayLocal(now: Date = new Date()): string {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** "yyyy-MM-dd" to a Date at local midnight. new Date("2026-11-13") would be UTC and can show the wrong day. */
export function parseDate(value: string): Date {
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function addDays(value: string, days: number): string {
  const date = parseDate(value);
  date.setDate(date.getDate() + days);
  return todayLocal(date);
}

export function daysBetween(from: string, to: string): number {
  const ms = parseDate(to).getTime() - parseDate(from).getTime();
  return Math.round(ms / 86_400_000);
}

/** "13 Nov", with the year only when it is not the current one. */
export function formatDate(value: string, today: string = todayLocal()): string {
  const date = parseDate(value);
  const sameYear = date.getFullYear() === parseDate(today).getFullYear();
  return new Intl.DateTimeFormat(LOCALE, {
    day: 'numeric',
    month: 'short',
    ...(sameYear ? {} : { year: 'numeric' }),
  }).format(date);
}

export function formatLongDate(value: string): string {
  return new Intl.DateTimeFormat(LOCALE, { weekday: 'long', day: 'numeric', month: 'long' }).format(
    parseDate(value),
  );
}

export function formatDateTime(iso: string): string {
  return new Intl.DateTimeFormat(LOCALE, {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso));
}

export type DueTone = 'overdue' | 'today' | 'soon' | 'later';

/** How a due date reads on a reminder: "2 days overdue", "Today", "Tomorrow", "Fri 20 Nov". */
export function describeDue(due: string, today: string): { label: string; tone: DueTone } {
  const diff = daysBetween(today, due);
  if (diff < 0) {
    const n = -diff;
    return { label: n === 1 ? '1 day overdue' : `${n} days overdue`, tone: 'overdue' };
  }
  if (diff === 0) return { label: 'Today', tone: 'today' };
  if (diff === 1) return { label: 'Tomorrow', tone: 'soon' };
  const label = new Intl.DateTimeFormat(LOCALE, {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
  }).format(parseDate(due));
  return { label, tone: diff <= 7 ? 'soon' : 'later' };
}

/** "3 days ago" style, for the timeline and "last activity". */
export function timeAgo(iso: string, now: Date = new Date()): string {
  const seconds = Math.round((now.getTime() - new Date(iso).getTime()) / 1000);
  if (seconds < 60) return 'just now';
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.round(hours / 24);
  if (days < 14) return days === 1 ? 'yesterday' : `${days} days ago`;
  return new Intl.DateTimeFormat(LOCALE, { day: 'numeric', month: 'short' }).format(new Date(iso));
}

/** €60k to 75k. Falls back gracefully when only one end is known. */
export function formatSalary(
  min?: number | null,
  max?: number | null,
  currency = 'EUR',
): string | null {
  if (min == null && max == null) return null;
  const money = (n: number): string =>
    new Intl.NumberFormat(LOCALE, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
      notation: n >= 10_000 ? 'compact' : 'standard',
    }).format(n);
  if (min != null && max != null) {
    if (min === max) return money(min);
    return `${money(min)} to ${money(max).replace(/^\D+/, '')}`;
  }
  return min != null ? `From ${money(min)}` : `Up to ${money(max as number)}`;
}
