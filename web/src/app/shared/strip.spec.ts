import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Application } from '../core/models';
import { StripComponent } from './strip';

const base: Application = {
  id: '1',
  company: 'Contoso',
  roleTitle: 'Backend Engineer',
  source: 'LinkedIn',
  workMode: 'Hybrid',
  currency: 'EUR',
  sponsorship: 'Unknown',
  priority: 'Medium',
  status: 'Applied',
  appliedOn: '2026-11-06',
  location: 'Dublin',
  lastActivityAt: '2026-11-06T10:00:00Z',
  createdAt: '2026-11-06T10:00:00Z',
  isArchived: false,
  isStale: false,
};

@Component({
  imports: [StripComponent],
  template: '<at-strip [app]="app" (statusChange)="changed = $event" />',
})
class Host {
  app: Application = base;
  changed: string | null = null;
}

function render(app: Partial<Application> = {}) {
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.app = { ...base, ...app };
  fixture.detectChanges();
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

describe('StripComponent', () => {
  it('shows company, role and where the application stands', () => {
    const { el } = render();
    expect(el.querySelector('[role=heading]')?.textContent).toContain('Contoso');
    expect(el.textContent).toContain('Backend Engineer');
    expect(el.querySelector('.track')?.getAttribute('aria-label')).toBe('Stage 2 of 5: Applied');
  });

  it('marks stops up to the current stage as reached', () => {
    const { el } = render({ status: 'Interviewing' });
    expect(el.querySelectorAll('.track-stop.is-done')).toHaveLength(3);
    expect(el.querySelectorAll('.track-stop.is-now')).toHaveLength(1);
  });

  it('describes ended applications without pretending they reached a stage', () => {
    const { el } = render({ status: 'Rejected' });
    expect(el.querySelector('.track')?.getAttribute('aria-label')).toBe('Ended: Rejected');
    expect(el.querySelectorAll('.track-stop.is-done')).toHaveLength(0);
  });

  it('calls out sponsorship, going quiet and priority', () => {
    const { el } = render({ sponsorship: 'Offered', isStale: true, priority: 'High' });
    expect(el.textContent).toContain('Offers sponsorship');
    expect(el.textContent).toContain('Gone quiet');
    expect(el.textContent).toContain('High priority');
  });

  it('does not nag about quiet applications that have ended', () => {
    const { el } = render({ status: 'Rejected', isStale: true });
    expect(el.textContent).not.toContain('Gone quiet');
  });

  it('announces a status change picked from the select', () => {
    const { fixture, el } = render();
    const select = el.querySelector('select') as HTMLSelectElement;
    select.value = 'Screening';
    select.dispatchEvent(new Event('change'));
    expect(fixture.componentInstance.changed).toBe('Screening');
  });
});
