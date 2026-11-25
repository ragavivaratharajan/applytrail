import { ApplicationStatus } from './models';

/** The five stops on a strip's track, in the order an application normally moves through them. */
export const PIPELINE: readonly ApplicationStatus[] = [
  'Saved',
  'Applied',
  'Screening',
  'Interviewing',
  'Offer',
];
export const CLOSED: readonly ApplicationStatus[] = ['Rejected', 'Withdrawn', 'Ghosted'];

export const STATUS_LABEL: Record<ApplicationStatus, string> = {
  Saved: 'Saved',
  Applied: 'Applied',
  Screening: 'Screening',
  Interviewing: 'Interviewing',
  Offer: 'Offer',
  Rejected: 'Rejected',
  Withdrawn: 'Withdrawn',
  Ghosted: 'No reply',
};

/** One-line meaning, used for tooltips and the board lane headers. */
export const STATUS_HINT: Record<ApplicationStatus, string> = {
  Saved: 'Found it, not applied yet',
  Applied: 'Sent, waiting to hear back',
  Screening: 'Recruiter or phone screen',
  Interviewing: 'In interview rounds',
  Offer: 'You have an offer',
  Rejected: 'They said no',
  Withdrawn: 'You pulled out',
  Ghosted: 'Gone quiet, no answer',
};

export const statusClass = (status: ApplicationStatus): string => `st-${status.toLowerCase()}`;

export const isClosed = (status: ApplicationStatus): boolean => CLOSED.includes(status);

/** Position on the five-stop track, or -1 when the application has ended. */
export const stageIndex = (status: ApplicationStatus): number => PIPELINE.indexOf(status);
