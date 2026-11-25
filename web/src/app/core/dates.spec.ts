import {
  addDays,
  daysBetween,
  describeDue,
  formatDate,
  formatSalary,
  parseDate,
  timeAgo,
  todayLocal,
} from './dates';

describe('dates', () => {
  it('todayLocal uses the local calendar date, not UTC', () => {
    // 23:30 on 13 Nov in a UTC-8 zone is already 14 Nov in UTC; local parts must win.
    const lateEvening = new Date(2026, 10, 13, 23, 30);
    expect(todayLocal(lateEvening)).toBe('2026-11-13');
  });

  it('parses a date-only string as local midnight (no UTC day shift)', () => {
    const d = parseDate('2026-11-13');
    expect([d.getFullYear(), d.getMonth(), d.getDate(), d.getHours()]).toEqual([2026, 10, 13, 0]);
  });

  it('adds days across month and year boundaries', () => {
    expect(addDays('2026-11-28', 7)).toBe('2026-12-05');
    expect(addDays('2026-12-30', 3)).toBe('2027-01-02');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('counts whole days between dates, including over a daylight-saving change', () => {
    expect(daysBetween('2026-11-13', '2026-11-20')).toBe(7);
    expect(daysBetween('2026-03-28', '2026-03-30')).toBe(2); // clocks change in Ireland on 29 March 2026
    expect(daysBetween('2026-11-20', '2026-11-13')).toBe(-7);
  });

  it('describes due dates by how urgent they are', () => {
    const today = '2026-11-13';
    expect(describeDue('2026-11-11', today)).toEqual({ label: '2 days overdue', tone: 'overdue' });
    expect(describeDue('2026-11-12', today)).toEqual({ label: '1 day overdue', tone: 'overdue' });
    expect(describeDue('2026-11-13', today)).toEqual({ label: 'Today', tone: 'today' });
    expect(describeDue('2026-11-14', today)).toEqual({ label: 'Tomorrow', tone: 'soon' });
    expect(describeDue('2026-11-17', today).tone).toBe('soon');
    expect(describeDue('2026-12-01', today).tone).toBe('later');
  });

  it('shows the year only when it differs from the current one', () => {
    expect(formatDate('2026-11-06', '2026-11-13')).not.toMatch(/2026/);
    expect(formatDate('2025-11-06', '2026-11-13')).toMatch(/2025/);
  });

  it('formats salary ranges and handles missing ends', () => {
    expect(formatSalary(null, null)).toBeNull();
    expect(formatSalary(70000, 85000)).toMatch(/70.*85/);
    expect(formatSalary(70000, 85000)!.match(/€/g)).toHaveLength(1); // currency symbol is not repeated
    expect(formatSalary(60000, 60000)).toMatch(/60/);
    expect(formatSalary(60000, null)).toMatch(/^From/);
    expect(formatSalary(null, 60000)).toMatch(/^Up to/);
  });

  it('reads relative times', () => {
    const now = new Date('2026-11-13T12:00:00Z');
    expect(timeAgo('2026-11-13T11:59:30Z', now)).toBe('just now');
    expect(timeAgo('2026-11-13T11:15:00Z', now)).toBe('45 min ago');
    expect(timeAgo('2026-11-13T09:00:00Z', now)).toBe('3 h ago');
    expect(timeAgo('2026-11-12T12:00:00Z', now)).toBe('yesterday');
    expect(timeAgo('2026-11-08T12:00:00Z', now)).toBe('5 days ago');
  });
});
