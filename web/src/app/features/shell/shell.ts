import { ChangeDetectionStrategy, Component, HostListener, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { FormDialogService } from '../../core/form-dialog.service';
import { SettingsService } from '../../core/settings.service';
import { BrandComponent } from '../../shared/brand';
import { IconComponent } from '../../shared/icon';
import { ToastHostComponent } from '../../shared/toast-host';
import { ApplicationFormComponent } from '../applications/application-form';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'at-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    IconComponent,
    BrandComponent,
    ToastHostComponent,
    ApplicationFormComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-50 focus:rounded-md focus:bg-surface focus:px-3 focus:py-2"
      >Skip to content</a
    >

    <div class="min-h-screen lg:grid lg:grid-cols-[15rem_1fr]">
      <!-- Desktop rail -->
      <aside
        aria-label="Sidebar"
        class="hidden border-r border-line bg-surface lg:sticky lg:top-0 lg:flex lg:h-screen lg:flex-col lg:px-4 lg:py-5"
      >
        <a routerLink="/today" class="px-1" aria-label="ApplyTrail home"><at-brand /></a>

        <button
          type="button"
          class="btn btn-primary mt-6 w-full justify-between"
          (click)="form.openCreate()"
          title="New application (N)"
        >
          <span class="inline-flex items-center gap-2"
            ><at-icon name="plus" [size]="18" />New application</span
          >
          <kbd class="kbd !border-white/30 !bg-white/10 !text-inherit">N</kbd>
        </button>

        <nav aria-label="Main" class="mt-6 flex flex-col gap-1">
          @for (item of nav; track item.path) {
            <a [routerLink]="item.path" routerLinkActive="nav-active" class="nav-link">
              <span class="mark"></span>
              <at-icon [name]="item.icon" />
              {{ item.label }}
            </a>
          }
        </nav>

        <div class="mt-auto border-t border-line pt-4">
          <p class="truncate px-1 text-[0.8125rem] text-soft" [title]="auth.email() ?? ''">
            {{ auth.email() }}
          </p>
          <button
            type="button"
            class="btn btn-text mt-1 w-full justify-start"
            (click)="auth.logout()"
          >
            <at-icon name="logout" [size]="16" />Sign out
          </button>
        </div>
      </aside>

      <div class="min-w-0">
        <!-- Phone top bar -->
        <header
          class="sticky top-0 z-30 flex items-center justify-between border-b border-line bg-surface/95 px-4 py-2.5 backdrop-blur lg:hidden"
        >
          <a routerLink="/today" aria-label="ApplyTrail home"><at-brand /></a>
          <button type="button" class="btn btn-primary btn-sm" (click)="form.openCreate()">
            <at-icon name="plus" [size]="16" />New
          </button>
        </header>

        <main id="main" tabindex="-1" class="outline-none"><router-outlet /></main>
      </div>

      <!-- Phone tab bar -->
      <nav
        aria-label="Main"
        class="fixed inset-x-0 bottom-0 z-30 grid grid-cols-5 border-t border-line bg-surface lg:hidden"
      >
        @for (item of nav; track item.path) {
          <a [routerLink]="item.path" routerLinkActive="nav-active" class="tab-link">
            <span class="bar"></span>
            <at-icon [name]="item.icon" [size]="20" />
            {{ item.label }}
          </a>
        }
      </nav>
    </div>

    <at-application-form />
    <at-toast-host />
  `,
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly form = inject(FormDialogService);
  private readonly settings = inject(SettingsService);

  protected readonly nav: NavItem[] = [
    { path: '/today', label: 'Today', icon: 'today' },
    { path: '/board', label: 'Board', icon: 'board' },
    { path: '/applications', label: 'Applications', icon: 'list' },
    { path: '/insights', label: 'Insights', icon: 'chart' },
    { path: '/settings', label: 'Settings', icon: 'settings' },
  ];

  constructor() {
    void this.settings.load().catch(() => undefined);
  }

  /** Press N anywhere (outside a text field or dialog) to add an application. */
  @HostListener('document:keydown', ['$event'])
  protected onKey(event: KeyboardEvent): void {
    if (event.key.toLowerCase() !== 'n' || event.ctrlKey || event.metaKey || event.altKey) return;
    const target = event.target as HTMLElement | null;
    if (target?.closest('input, textarea, select, dialog, [contenteditable="true"]')) return;
    if (document.querySelector('dialog[open]')) return;
    event.preventDefault();
    this.form.openCreate();
  }
}
