import { ChangeDetectionStrategy, Component, DestroyRef, Injector, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { Params, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import {
  BulkAction,
  BulkUpdateResult,
  STAFF_TICKET_SORT_FIELDS,
  STAFF_TICKET_SORT_LABELS,
  StaffTicketListItem,
  StaffTicketSortField,
  TICKET_STATUS_VALUES,
  TicketStatus,
  URGENCY_FILTER_VALUES,
  UrgencyFilter,
  ACTIVE_TICKET_STATUSES,
} from '../../../models/ticket.models';
import { StatusLabelPipe } from '../../../pipes/status-label.pipe';
import { urgencyLabel } from '../../../pipes/urgency-label.pipe';
import { ClockService } from '../../../services/clock.service';
import { isOverdueNow } from '../../../services/due-countdown';
import { NotificationService } from '../../../services/notification.service';
import { defaultDir } from '../../../services/list-state';
import {
  TICKET_QUEUE_LIST,
  TicketQueueStore,
  includesFinalStatuses,
  withFinalStatuses,
} from '../../../services/ticket-queue.store';
import { ClassificationSourceBadge } from '../../shared/classification-source-badge/classification-source-badge';
import {
  DataTable,
  DataTableCell,
  DataTableColumn,
  DataTableHeaderCell,
  DataTableRowCard,
} from '../../shared/data-table/data-table';
import { DueCountdown } from '../../shared/due-countdown/due-countdown';
import { EmptyState } from '../../shared/empty-state/empty-state';
import { FilterBar } from '../../shared/filter-bar/filter-bar';
import { PageHeader } from '../../shared/page-header/page-header';
import { SearchField } from '../../shared/search-field/search-field';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { NOT_SET_LABEL, UrgencyBadge } from '../../shared/urgency-badge/urgency-badge';
import { BULK_FAILED_MESSAGE, CONFLICT_MESSAGE, TAKEN_MESSAGE, TAKE_FAILED_MESSAGE } from '../handler-messages';
import { HandlerNavigationState, QUEUE_TABS, handlerTicketUrl, queueTabUrl } from '../handler-urls';
import { BulkActionBar } from './bulk-action-bar/bulk-action-bar';
import { openBulkAssignDialog } from './bulk-assign-dialog/bulk-assign-dialog';
import { openBulkResultDialog } from './bulk-result-dialog/bulk-result-dialog';

const EMPTY_TEXT: Record<TicketQueueStore['scope'], { heading: string; description: string }> = {
  assigned: { heading: 'אין פניות משויכות אליך', description: 'פניות שישויכו אליך יופיעו כאן, הדחופות ביותר בראש.' },
  queue: { heading: 'התור ריק', description: 'אין במחלקה פניות שממתינות למטפל.' },
  handling: { heading: 'אין פניות בטיפול המחלקה', description: 'פניות פתוחות שהמחלקה מטפלת בהן יופיעו כאן.' },
  department: { heading: 'אין פניות', description: '' },
};

/**
 * The handler's queue (spec 7.6): three tabs (each a route and a scope of the API), filters in the URL, sorted by
 * "עדיפות" (decision in CLAUDE.md, instead of the due time of 7.6) and without closed or cancelled tickets by default. Overdue rows are red with an icon and the word "חריגה" (not colour alone – spec 11.6).
 * "קח לטיפולי" on every open row of the department queue. Narrow screens: one card per ticket.
 * Bulk actions (task 3.6): a checkbox on every row (and card) and "select all" of the current page only; another page,
 * filter, sort or tab clears the selection. While tickets are selected, a bar offers "התחל טיפול" and "העברה"; the
 * result dialog lists the tickets that failed with the reason, and those stay selected.
 */
@Component({
  selector: 'app-ticket-queue-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatTabsModule,
    PageHeader,
    FilterBar,
    SearchField,
    DataTable,
    DataTableCell,
    DataTableHeaderCell,
    DataTableRowCard,
    BulkActionBar,
    EmptyState,
    StatusBadge,
    UrgencyBadge,
    DueCountdown,
    ClassificationSourceBadge,
    StatusLabelPipe,
  ],
  providers: [TicketQueueStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-queue-page.html',
  styleUrl: './ticket-queue-page.scss',
})
export class TicketQueuePage {
  protected readonly store = inject(TicketQueueStore);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly clock = inject(ClockService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = inject(MatDialog);
  private readonly injector = inject(Injector);

  protected readonly list = this.store.list;
  protected readonly state = this.list.state;
  protected readonly tabs = QUEUE_TABS;
  protected readonly isQueueTab = this.store.scope === 'queue';
  protected readonly emptyText = EMPTY_TEXT[this.store.scope];
  protected readonly statusOptions = TICKET_STATUS_VALUES;
  protected readonly urgencyOptions = URGENCY_FILTER_VALUES;
  protected readonly sortOptions = STAFF_TICKET_SORT_FIELDS;
  protected readonly sortLabels = STAFF_TICKET_SORT_LABELS;
  protected readonly includesFinal = computed(() => includesFinalStatuses(this.state().filters.status));
  protected readonly ticketUrl = handlerTicketUrl;
  protected readonly queueTabUrl = queueTabUrl;

  protected readonly columns: readonly DataTableColumn[] = [
    { key: 'select', header: 'בחירה' },
    { key: 'id', header: "מס'" },
    { key: 'subject', header: 'נושא' },
    { key: 'requester', header: 'שם העובד' },
    { key: 'requesterDepartment', header: 'מחלקת העובד' },
    { key: 'category', header: 'קטגוריה' },
    { key: 'urgency', header: 'דחיפות', sortKey: 'urgency', firstDir: 'desc' },
    { key: 'status', header: 'סטטוס', sortKey: 'status' },
    { key: 'dueAt', header: 'מועד יעד', sortKey: 'dueAt' },
    { key: 'source', header: 'מקור סיווג' },
    ...(this.isQueueTab ? [{ key: 'actions', header: 'פעולות' }] : []),
  ];

  /** Filters other than the tab, kept when switching tabs (back to page 1). */
  protected readonly tabQueryParams = computed<Params>(() => {
    const params = { ...this.list.queryParams() };
    delete params['page'];
    return params;
  });

  /**
   * Overdue now: the server's flag, or the due time passed since the list was loaded (the clock moves on).
   * Selected for a bulk action: "row-selected" too.
   */
  protected readonly rowClass = (ticket: StaffTicketListItem): string | null => {
    const classes = [
      isOverdueNow(ticket.dueAt, ticket.status, ticket.isOverdue, this.clock.now()) ? 'row-overdue' : null,
      this.store.isSelected(ticket.id) ? 'row-selected' : null,
    ].filter((name) => name !== null);
    return classes.length > 0 ? classes.join(' ') : null;
  };

  private readonly bulkAction = signal<BulkAction | null>(null);
  /** The bulk action that is running (its button shows a spinner), or null. */
  protected readonly runningBulkAction = computed(() => (this.store.bulkBusy() ? this.bulkAction() : null));

  protected isOverdue(ticket: StaffTicketListItem): boolean {
    return isOverdueNow(ticket.dueAt, ticket.status, ticket.isOverdue, this.clock.now());
  }

  /** The queue's button is shown for tickets that can still be assigned; the server decides. */
  protected canTake(ticket: StaffTicketListItem): boolean {
    return this.isQueueTab && ACTIVE_TICKET_STATUSES.includes(ticket.status);
  }

  protected urgencyOptionLabel(value: UrgencyFilter): string {
    return value === 'NotSet' ? NOT_SET_LABEL : urgencyLabel(value);
  }

  protected onSearch(text: string): void {
    this.list.setFilters({ search: text || null }, { replaceUrl: true });
  }

  protected onStatuses(values: TicketStatus[]): void {
    this.list.setFilters({ status: values.length > 0 ? values : null });
  }

  /** "כולל סגורות ומבוטלות": adds or removes Closed and Cancelled in the status filter. */
  protected onIncludeFinal(checked: boolean): void {
    this.list.setFilters({ status: withFinalStatuses(this.state().filters.status, checked) });
  }

  /** The sort select: the field in its usual direction (a column header still toggles it). */
  protected onSortBy(by: StaffTicketSortField): void {
    this.list.setSort({ by, dir: defaultDir(by, TICKET_QUEUE_LIST) });
  }

  protected onUrgencies(values: UrgencyFilter[]): void {
    this.list.setFilters({ urgency: values.length > 0 ? values : null });
  }

  protected onCategory(value: number | null): void {
    this.list.setFilters({ categoryId: value });
  }

  protected onDate(field: 'from' | 'to', value: string): void {
    const day = value || null;
    this.list.setFilters(field === 'from' ? { from: day } : { to: day });
  }

  protected onOverdueOnly(checked: boolean): void {
    this.list.setFilters({ overdueOnly: checked ? true : null });
  }

  protected navigationState(): HandlerNavigationState {
    return { queueUrl: this.router.url };
  }

  protected openTicket(ticket: StaffTicketListItem): void {
    void this.router.navigateByUrl(handlerTicketUrl(ticket.id), { state: this.navigationState() });
  }

  /** "התחל טיפול" on the selected tickets, then the result dialog. */
  protected startSelected(): void {
    this.bulkAction.set('start');
    this.store
      .bulkStart()
      .pipe(
        finalize(() => this.bulkAction.set(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (result) => this.showBulkResult('start', result),
        error: () => this.notifications.error(BULK_FAILED_MESSAGE),
      });
  }

  /** "העברה…": the dialog sends the request (it shows its own errors) and closes with the result. */
  protected assignSelected(): void {
    this.bulkAction.set('assign');
    openBulkAssignDialog(this.dialog, this.injector)
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        this.bulkAction.set(null);
        if (result) {
          this.showBulkResult('assign', result);
        }
      });
  }

  private showBulkResult(action: BulkAction, result: BulkUpdateResult): void {
    openBulkResultDialog(this.dialog, { action, result, queueUrl: this.router.url });
  }

  protected take(ticket: StaffTicketListItem): void {
    this.store
      .take(ticket)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (result === 'taken') {
          this.notifications.success(TAKEN_MESSAGE);
        } else {
          this.notifications.error(result === 'conflict' ? CONFLICT_MESSAGE : TAKE_FAILED_MESSAGE);
        }
      });
  }
}
