import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  TemplateRef,
  computed,
  contentChildren,
  inject,
  input,
  output,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorIntl, MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { PAGE_SIZE_OPTIONS } from '../../../core/models/paging.models';
import { HebrewPaginatorIntl } from '../../i18n/hebrew-paginator-intl';
import { ListSort } from '../../list/list-state';
import { LoadStatus } from '../../list/list-store';
import { ErrorState } from '../error-state/error-state';
import { LoadingState } from '../loading-state/loading-state';

export interface DataTableColumn {
  key: string;
  header: string;
  /** The server's sortBy value; omitted → not sortable. */
  sortKey?: string;
  align?: 'start' | 'center' | 'end';
}

export interface DataTablePage {
  /** 1-based. */
  page: number;
  pageSize: number;
}

/**
 * The content of one column's cells: <ng-template appCell="email" let-row>{{ row.email }}</ng-template>.
 * A column without a template shows row[key] as text.
 */
@Directive({ selector: 'ng-template[appCell]' })
export class DataTableCell {
  readonly key = input.required<string>({ alias: 'appCell' });
  readonly template = inject<TemplateRef<{ $implicit: unknown }>>(TemplateRef);
}

/**
 * Table in a card with server-side sort and paging (spec 9.4) and the view states of 7.10.
 * The table never sorts or pages by itself: it emits sortChange / pageChange and shows what it is given
 * (typically a ListStore's state). The "empty" content is projected with the tableEmpty attribute, so the screen
 * decides between "no data" and "no results".
 */
@Component({
  selector: 'app-data-table',
  imports: [NgTemplateOutlet, MatIconModule, MatPaginatorModule, MatProgressBarModule, LoadingState, ErrorState],
  providers: [{ provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl }],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './data-table.html',
  styleUrl: './data-table.scss',
})
export class DataTable<T> {
  readonly columns = input.required<readonly DataTableColumn[]>();
  readonly rows = input.required<readonly T[]>();
  readonly status = input.required<LoadStatus>();
  /** Describes the table for screen readers, e.g. "רשימת העובדים". */
  readonly caption = input.required<string>();
  readonly sort = input<ListSort | null>(null);
  readonly paginated = input(true);
  readonly totalCount = input(0);
  readonly page = input(1);
  readonly pageSize = input(25);
  /** Rows react to a click (e.g. open the details). Keyboard users use the link inside the row. */
  readonly rowClickable = input(false);
  readonly trackBy = input<(row: T) => unknown>((row) => (row as { id?: unknown }).id ?? row);

  readonly sortChange = output<ListSort>();
  readonly pageChange = output<DataTablePage>();
  readonly rowActivate = output<T>();
  readonly retry = output<void>();

  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  private readonly cells = contentChildren(DataTableCell);
  protected readonly templates = computed(() => new Map(this.cells().map((cell) => [cell.key(), cell.template])));

  protected readonly view = computed<'loading' | 'error' | 'empty' | 'data'>(() => {
    if (this.status() === 'error') {
      return 'error';
    }
    if (this.rows().length === 0) {
      return this.status() === 'loading' ? 'loading' : 'empty';
    }
    return 'data';
  });

  /** Reloading while rows are shown: keep them, with a progress bar. */
  protected readonly refreshing = computed(() => this.status() === 'loading' && this.rows().length > 0);

  protected readonly resultAnnouncement = computed(() => {
    if (this.status() !== 'ready') {
      return '';
    }
    const count = this.paginated() ? this.totalCount() : this.rows().length;
    return count === 1 ? 'נמצאה תוצאה אחת' : `נמצאו ${count} תוצאות`;
  });

  protected toggleSort(column: DataTableColumn): void {
    if (!column.sortKey) {
      return;
    }
    const current = this.sort();
    const dir = current?.by === column.sortKey && current.dir === 'asc' ? 'desc' : 'asc';
    this.sortChange.emit({ by: column.sortKey, dir });
  }

  protected ariaSort(column: DataTableColumn): string | null {
    if (!column.sortKey) {
      return null;
    }
    const current = this.sort();
    if (current?.by !== column.sortKey) {
      return 'none';
    }
    return current.dir === 'asc' ? 'ascending' : 'descending';
  }

  protected sortIcon(column: DataTableColumn): string {
    const current = this.sort();
    if (!current || current.by !== column.sortKey) {
      return 'unfold_more';
    }
    return current.dir === 'asc' ? 'arrow_upward' : 'arrow_downward';
  }

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected onRowClick(row: T, event: MouseEvent): void {
    if (!this.rowClickable()) {
      return;
    }
    // A link or a button inside the row does its own thing.
    const target = event.target as HTMLElement | null;
    if (target?.closest('a, button, input, select, textarea, [role="button"], [role="menuitem"]')) {
      return;
    }
    this.rowActivate.emit(row);
  }

  protected cellText(row: T, key: string): string {
    const value = (row as Record<string, unknown>)[key];
    return value === null || value === undefined ? '' : String(value);
  }
}
