import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const isApiCall = (url: string): boolean =>
  url.startsWith('/api/') && !url.startsWith('/api/auth/');

/** Adds the bearer token, and on a 401 refreshes the session once and replays the request. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiCall(request.url)) return next(request);

  const auth = inject(AuthService);
  const withToken = (req: HttpRequest<unknown>): HttpRequest<unknown> => {
    const token = auth.accessToken();
    return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  };

  return next(withToken(request)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401)
        return throwError(() => error);

      return from(auth.refresh()).pipe(
        switchMap((refreshed) => {
          if (refreshed) return next(withToken(request));
          // Only a rejected refresh token (which clears the session) ends it; a network blip must not sign anyone out.
          if (!auth.isAuthenticated()) void auth.goToLogin();
          return throwError(() => error);
        }),
      );
    }),
  );
};
