import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../core/toast.service';
import { IconComponent } from './icon';

@Component({
  selector: 'at-toast-host',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pointer-events-none fixed inset-x-0 bottom-20 z-50 flex flex-col items-center gap-2 px-4 lg:bottom-6"
      aria-live="polite"
      role="status"
    >
      @for (toast of toasts.toasts(); track toast.id) {
        <div
          class="pointer-events-auto flex max-w-md items-start gap-3 rounded-lg px-4 py-2.5 text-[0.9375rem] shadow-lg"
          [class]="toast.kind === 'error' ? 'bg-danger text-white' : 'bg-primary text-on-primary'"
        >
          <span>{{ toast.message }}</span>
          <button
            type="button"
            class="-mr-1 mt-0.5 opacity-80 hover:opacity-100"
            aria-label="Dismiss"
            (click)="toasts.dismiss(toast.id)"
          >
            <at-icon name="x" [size]="14" />
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastHostComponent {
  protected readonly toasts = inject(ToastService);
}
