import { tidyJobUrl } from './job-url';

describe('tidyJobUrl', () => {
  it('collapses a long LinkedIn search-results link to the job itself', () => {
    const pasted =
      'https://www.linkedin.com/jobs/search-results/?currentJobId=4490460225&eBP=' +
      'CwEAAAGeQ'.repeat(120) +
      '&refId=abc%3D%3D&trackingId=xyz%3D%3D&origin=JOB_SEARCH_PAGE_JOB_FILTER';
    expect(pasted.length).toBeGreaterThan(500);
    expect(tidyJobUrl(pasted)).toBe('https://www.linkedin.com/jobs/view/4490460225/');
  });

  it('understands the other LinkedIn job links', () => {
    const clean = 'https://www.linkedin.com/jobs/view/123456789/';
    expect(
      tidyJobUrl('https://www.linkedin.com/jobs/collections/recommended/?currentJobId=123456789'),
    ).toBe(clean);
    expect(tidyJobUrl('https://ie.linkedin.com/jobs/view/123456789?trk=public_jobs&refId=x')).toBe(
      clean,
    );
    expect(
      tidyJobUrl('https://www.linkedin.com/jobs/view/senior-net-engineer-at-acme-123456789'),
    ).toBe(clean);
    expect(tidyJobUrl('https://www.linkedin.com/comm/jobs/view/123456789/?trackingId=x')).toBe(
      clean,
    );
  });

  it('leaves a link that is already clean exactly as it was', () => {
    const clean = 'https://www.linkedin.com/jobs/view/123456789/';
    expect(tidyJobUrl(clean)).toBe(clean);
    expect(tidyJobUrl('  https://boards.greenhouse.io/acme/jobs/42  ')).toBe(
      'https://boards.greenhouse.io/acme/jobs/42',
    );
  });

  it('removes tracking parameters from other sites but keeps the ones that identify the job', () => {
    expect(
      tidyJobUrl('https://ie.indeed.com/viewjob?jk=abc123&utm_source=share&fbclid=zzz&gclid=1'),
    ).toBe('https://ie.indeed.com/viewjob?jk=abc123');
    expect(tidyJobUrl('https://example.com/jobs/42?utm_medium=email')).toBe(
      'https://example.com/jobs/42',
    );
  });

  it('does not touch something that is not an http(s) link', () => {
    expect(tidyJobUrl(' not a link ')).toBe('not a link');
    expect(tidyJobUrl('ftp://example.com/job')).toBe('ftp://example.com/job');
    expect(tidyJobUrl('')).toBe('');
  });

  it('keeps a LinkedIn link that is not a job page', () => {
    expect(tidyJobUrl('https://www.linkedin.com/company/acme/jobs/')).toBe(
      'https://www.linkedin.com/company/acme/jobs/',
    );
  });
});
