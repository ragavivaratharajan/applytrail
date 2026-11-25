import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

// inject() only works before the first await, so the services are taken first.

export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.start();
  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], {
        queryParams: state.url === '/' ? {} : { returnUrl: state.url },
      });
};

/** Sign-in and sign-up pages bounce people who are already signed in. */
export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.start();
  return auth.isAuthenticated() ? router.createUrlTree(['/']) : true;
};
