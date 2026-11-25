import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';

/** A native <dialog> confirmation. open() resolves true only when the person confirms. */
@Component({
  selector: 'at-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      #dialog
      class="m-auto w-[min(26rem,calc(100vw-2rem))] rounded-2xl border border-line bg-surface p-0 shadow-2xl"
      [attr.aria-labelledby]="'confirm-title'"
    >
      <form method="dialog" class="p-5">
        <h2 id="confirm-title" class="text-xl font-semibold">{{ heading() }}</h2>
        <p class="mt-2 text-soft">{{ message() }}</p>
        <div class="mt-5 flex justify-end gap-2">
          <button class="btn btn-quiet" value="cancel" autofocus>Cancel</button>
          <button [class]="danger() ? 'btn btn-danger' : 'btn btn-primary'" value="ok">
            {{ confirmLabel() }}
          </button>
        </div>
      </form>
    </dialog>
  `,
})
export class ConfirmDialogComponent {
  readonly heading = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmLabel = input('Confirm');
  readonly danger = input(false);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  open(): Promise<boolean> {
    const el = this.dialog().nativeElement;
    return new Promise((resolve) => {
      el.addEventListener('close', () => resolve(el.returnValue === 'ok'), { once: true });
      el.returnValue = 'cancel';
      el.showModal();
    });
  }
}
