import { ApplicationStatus, STATUSES } from './models';
import { CLOSED, PIPELINE, STATUS_LABEL, isClosed, stageIndex, statusClass } from './status';

describe('status helpers', () => {
  it('places each open status on the five-stop track', () => {
    expect(PIPELINE.map(stageIndex)).toEqual([0, 1, 2, 3, 4]);
  });

  it('puts ended applications off the track', () => {
    for (const s of CLOSED) {
      expect(stageIndex(s)).toBe(-1);
      expect(isClosed(s)).toBe(true);
    }
    expect(isClosed('Offer')).toBe(false);
  });

  it('covers every status exactly once between the track and the closed set', () => {
    expect([...PIPELINE, ...CLOSED].sort()).toEqual([...STATUSES].sort());
  });

  it('gives every status a label and a css class', () => {
    for (const s of STATUSES as readonly ApplicationStatus[]) {
      expect(STATUS_LABEL[s]).toBeTruthy();
      expect(statusClass(s)).toBe(`st-${s.toLowerCase()}`);
    }
    expect(STATUS_LABEL.Ghosted).toBe('No reply'); // plain words for what the person sees
  });
});
