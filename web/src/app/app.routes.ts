import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · ApplyTrail',
    data: { mode: 'login' },
    loadComponent: () => import('./features/auth/auth').then((m) => m.AuthComponent),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create account · ApplyTrail',
    data: { mode: 'register' },
    loadComponent: () => import('./features/auth/auth').then((m) => m.AuthComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/shell/shell').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'today' },
      {
        path: 'today',
        title: 'Today · ApplyTrail',
        loadComponent: () => import('./features/today/today').then((m) => m.TodayComponent),
      },
      {
        path: 'board',
        title: 'Board · ApplyTrail',
        loadComponent: () => import('./features/board/board').then((m) => m.BoardComponent),
      },
      {
        path: 'applications',
        title: 'Applications · ApplyTrail',
        loadComponent: () =>
          import('./features/applications/applications').then((m) => m.ApplicationsComponent),
      },
      {
        path: 'applications/:id',
        title: 'Application · ApplyTrail',
        loadComponent: () =>
          import('./features/applications/application-detail').then(
            (m) => m.ApplicationDetailComponent,
          ),
      },
      {
        path: 'insights',
        title: 'Insights · ApplyTrail',
        loadComponent: () =>
          import('./features/insights/insights').then((m) => m.InsightsComponent),
      },
      {
        path: 'settings',
        title: 'Settings · ApplyTrail',
        loadComponent: () =>
          import('./features/settings/settings').then((m) => m.SettingsComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'today' },
];
