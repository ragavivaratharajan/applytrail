import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api-error';
import { toParams } from './api.service';

describe('toParams', () => {
  it('repeats the key for arrays, the way ASP.NET binds them', () => {
    const params = toParams({ status: ['Applied', 'Screening'], page: 2 });
    expect(params.getAll('status')).toEqual(['Applied', 'Screening']);
    expect(params.get('page')).toBe('2');
  });

  it('leaves out empty values so the URL stays clean', () => {
    const params = toParams({
      q: '',
      source: '',
      priority: undefined,
      archived: undefined,
      dir: 'desc',
    });
    expect(params.keys()).toEqual(['dir']);
  });

  it('keeps an explicit false', () => {
    expect(toParams({ archived: false }).get('archived')).toBe('false');
  });
});

describe('ApiError.from', () => {
  it('turns a failed connection into a plain explanation', () => {
    const error = ApiError.from(new HttpErrorResponse({ status: 0 }));
    expect(error.status).toBe(0);
    expect(error.messages.join(' ')).toMatch(/Cannot reach the server/);
  });

  it('reads problem details', () => {
    const error = ApiError.from(
      new HttpErrorResponse({
        status: 409,
        error: { title: 'An account with this email already exists.' },
      }),
    );
    expect(error.status).toBe(409);
    expect(error.messages).toEqual(['An account with this email already exists.']);
  });

  it('collects validation messages per field with camelCase keys', () => {
    const error = ApiError.from(
      new HttpErrorResponse({
        status: 400,
        error: {
          title: 'Validation failed',
          errors: { Password: ['Password must be 10 to 100 characters.'] },
        },
      }),
    );
    expect(Object.keys(error.fieldErrors)).toEqual(['password']);
    expect(error.messages).toEqual(['Password must be 10 to 100 characters.']);
  });

  it('explains rate limiting', () => {
    const error = ApiError.from(new HttpErrorResponse({ status: 429, error: null }));
    expect(error.messages[0]).toMatch(/Too many attempts/);
  });

  it('never leaks a raw exception', () => {
    expect(ApiError.from(new Error('secret stack trace')).messages).toEqual([
      'Something went wrong.',
    ]);
  });
});
