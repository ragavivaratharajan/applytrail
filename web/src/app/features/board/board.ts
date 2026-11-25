import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ApiService } from '../../core/api.service';
import { errorMessage } from '../../core/api-error';
import { DataStore } from '../../core/data-store';
import { todayLocal } from '../../core/dates';
import { FormDialogService } from '../../core/form-dialog.service';
import { Application, ApplicationStatus, STATUSES } from '../../core/models';
import { CLOSED, STATUS_HINT, STATUS_LABEL, statusClass } from '../../core/status';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon';
import { StripComponent } from '../../shared/strip';

const BOARD_LIMIT = 200;

/** Every live application in a lane for its stage. Drag a strip to another lane to change its status. */
@Component({
  selector: 'at-board',
  imports: [CdkDropListGroup, CdkDropList, CdkDrag, StripComponent, IconComponent],
  templateUrl: './board.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoardComponent {
  private readonly api = inject(ApiService);
  private readonly store = inject(DataStore);
  private readonly toast = inject(ToastService);
  protected readonly formDialog = inject(FormDialogService);

  protected readonly items = signal<Application[]>([]);
  protected readonly total = signal(0);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly limit = BOARD_LIMIT;

  protected readonly statusLabel = STATUS_LABEL;
  protected readonly statusHint = STATUS_HINT;
  protected readonly statusClass = statusClass;
  protected readonly dragDelay = { touch: 300, mouse: 0 };

  /** On touch screens, show each strip's status picker (dragging is awkward there). */
  protected readonly hideStatus = !(
    typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches
  );

  protected readonly lanes = computed(() =>
    STATUSES.map((status) => ({
      status,
      closed: CLOSED.includes(status),
      items: this.items().filter((a) => a.status === status),
    })),
  );

  constructor() {
    effect(() => {
      this.store.version();
      void this.load();
    });
  }

  protected async drop(
    event: CdkDragDrop<Application[]>,
    target: ApplicationStatus,
  ): Promise<void> {
    if (event.previousContainer === event.container) return;
    const moved = event.item.data as Application;
    if (moved.status === target) return;
    await this.move(moved, target);
  }

  /** Also used by the status picker on each strip, so keyboard and touch users get the same behaviour. */
  protected async move(app: Application, target: ApplicationStatus): Promise<void> {
    const previous = app.status;
    this.replace({ ...app, status: target }); // show it in the new lane straight away

    try {
      const saved = await this.api.changeStatus(app.id, target, todayLocal());
      this.replace(saved);
      this.toast.show(`${app.company} moved to ${STATUS_LABEL[target]}`);
      this.store.invalidate();
    } catch (error) {
      this.replace({ ...app, status: previous });
      this.toast.error(errorMessage(error));
    }
  }

  private replace(updated: Application): void {
    this.items.update((list) => list.map((a) => (a.id === updated.id ? updated : a)));
  }

  private async load(): Promise<void> {
    try {
      const page = await this.api.list({
        sort: 'lastActivity',
        dir: 'desc',
        page: 1,
        pageSize: BOARD_LIMIT,
      });
      this.items.set(page.items);
      this.total.set(page.total);
      this.error.set(null);
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.loaded.set(true);
    }
  }
}
