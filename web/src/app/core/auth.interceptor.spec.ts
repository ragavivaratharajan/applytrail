import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

const session = (token: string) => ({
  accessToken: token,
  expiresAt: '2026-11-13T12:15:00Z',
  email: 'a@b.ie',
});

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);

    // sign in first so there is a token in memory
    const login = auth.login('a@b.ie', 'pw');
    backend.expectOne('/api/auth/login').flush(session('token-1'));
    await login;
  });

  afterEach(() => backend.verify());

  it('adds the bearer token to API calls', () => {
    http.get('/api/applications').subscribe();
    const req = backend.expectOne('/api/applications');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-1');
    req.flush({});
  });

  it('does not attach the token to auth endpoints or other hosts', () => {
    http.post('/api/auth/login', {}).subscribe();
    expect(backend.expectOne('/api/auth/login').request.headers.has('Authorization')).toBe(false);
    http.get('https://example.com/data').subscribe();
    expect(backend.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(
      false,
    );
  });

  it('refreshes once on a 401 and replays the request with the new token', async () => {
    const result = new Promise((resolve) => http.get('/api/dashboard').subscribe(resolve));

    backend
      .expectOne('/api/dashboard')
      .flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve();
    backend.expectOne('/api/auth/refresh').flush(session('token-2'));
    await new Promise((r) => setTimeout(r));

    const retry = backend.expectOne('/api/dashboard');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
    retry.flush({ ok: true });
    await expect(result).resolves.toEqual({ ok: true });
  });

  it('shares one refresh between requests that fail together', async () => {
    http.get('/api/a').subscribe();
    http.get('/api/b').subscribe();
    backend.expectOne('/api/a').flush({}, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('/api/b').flush({}, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve();

    const refreshes = backend.match('/api/auth/refresh');
    expect(refreshes).toHaveLength(1);
    refreshes[0].flush(session('token-2'));
    await new Promise((r) => setTimeout(r));

    backend.expectOne('/api/a').flush({});
    backend.expectOne('/api/b').flush({});
  });

  it('sends the person to sign in when the session cannot be renewed, and remembers where they were', async () => {
    let failure: unknown;
    http.get('/api/dashboard').subscribe({ error: (e) => (failure = e) });
    backend.expectOne('/api/dashboard').flush({}, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve();
    backend
      .expectOne('/api/auth/refresh')
      .flush({ title: 'Session expired.' }, { status: 401, statusText: 'Unauthorized' });
    await new Promise((r) => setTimeout(r));

    expect(auth.isAuthenticated()).toBe(false);
    expect(failure).toBeTruthy();
    expect(router.navigate).toHaveBeenCalledWith(
      ['/login'],
      expect.objectContaining({ queryParams: expect.any(Object) }),
    );
    backend.expectNone('/api/dashboard');
  });

  it('keeps the session when the refresh fails only because the network is down', async () => {
    http.get('/api/dashboard').subscribe({ error: () => undefined });
    backend.expectOne('/api/dashboard').flush({}, { status: 401, statusText: 'Unauthorized' });
    await Promise.resolve();
    backend.expectOne('/api/auth/refresh').error(new ProgressEvent('error'), { status: 0 });
    await new Promise((r) => setTimeout(r));

    expect(auth.isAuthenticated()).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('does not retry other errors', () => {
    http.get('/api/dashboard').subscribe({ error: () => undefined });
    backend.expectOne('/api/dashboard').flush({}, { status: 500, statusText: 'Server Error' });
    backend.expectNone('/api/auth/refresh');
  });
});
