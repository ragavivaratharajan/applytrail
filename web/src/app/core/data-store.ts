import { Injectable, signal } from '@angular/core';

/**
 * Screens reload when this number changes. Anything that edits data (the form dialog, a status change)
 * calls invalidate(), so every open screen stays current without wiring them to each other.
 */
@Injectable({ providedIn: 'root' })
export class DataStore {
  readonly version = signal(0);

  invalidate(): void {
    this.version.update((v) => v + 1);
  }
}
