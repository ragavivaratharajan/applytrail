import { Injectable, signal } from '@angular/core';
import { Application } from './models';

export type FormTarget = { mode: 'create' } | { mode: 'edit'; application: Application };

/** Lets any screen open the add/edit dialog that lives in the shell. */
@Injectable({ providedIn: 'root' })
export class FormDialogService {
  readonly target = signal<FormTarget | null>(null);

  openCreate(): void {
    this.target.set({ mode: 'create' });
  }

  openEdit(application: Application): void {
    this.target.set({ mode: 'edit', application });
  }

  close(): void {
    this.target.set(null);
  }
}
