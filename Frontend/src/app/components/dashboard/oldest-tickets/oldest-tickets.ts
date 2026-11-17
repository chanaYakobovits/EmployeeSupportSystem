import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';

import { DashboardTicket } from '../../../models/dashboard.models';
import { ClockService } from '../../../services/clock.service';
import { isOverdueNow } from '../../../services/due-countdown';
import { HandlerNavigationState } from '../../handler/handler-urls';
import { DataTable, DataTableCell, DataTableColumn, DataTableRowCard } from '../../shared/data-table/data-table';
import { DueCountdown } from '../../shared/due-countdown/due-countdown';
import { StatusBadge } from '../../shared/status-badge/status-badge';
import { dashboardTicketUrl } from '../dashboard-urls';

/** Which department a row shows: the handling one (employees' tickets), the requester's (handled), or both. */
export type OldestTicketsDepartment = 'assigned' | 'requester' | 'both';

/**
 * "10 הפניות הפתוחות הוותיקות / בחריגה" (spec 7.9): overdue first, then the oldest – the server's order.
 * The number opens the handling screen inside the dashboard area (a manager / executive may not be a Handler); "back"
 * returns to this dashboard with its tab and range. Overdue: the row is red and says "חריגה" (not colour alone).
 */
@Component({
  selector: 'app-oldest-tickets',
  imports: [RouterLink, MatIconModule, DataTable, DataTableCell, DataTableRowCard, StatusBadge, DueCountdown],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './oldest-tickets.html',
  styleUrl: './oldest-tickets.scss',
})
export class OldestTickets {
  readonly tickets = input.required<readonly DashboardTicket[]>();
  readonly department = input<OldestTicketsDepartment>('both');

  private readonly router = inject(Router);
  private readonly clock = inject(ClockService);

  protected readonly ticketUrl = dashboardTicketUrl;

  protected readonly columns = computed<DataTableColumn[]>(() => {
    const department = this.department();
    return [
      { key: 'id', header: 'מספר' },
      { key: 'subject', header: 'נושא' },
      ...(department !== 'assigned' ? [{ key: 'requesterDepartment', header: 'מחלקת הפונה' }] : []),
      ...(department !== 'requester' ? [{ key: 'assignedDepartment', header: 'מחלקה מטפלת' }] : []),
      { key: 'status', header: 'סטטוס' },
      { key: 'dueAt', header: 'מועד יעד' },
    ];
  });

  protected readonly rowClass = (ticket: DashboardTicket): string | null => (this.isOverdue(ticket) ? 'row-overdue' : null);

  /** "Back" from the handling screen: this dashboard as it is now (tab + range). */
  protected navigationState(): HandlerNavigationState {
    return { queueUrl: this.router.url };
  }

  protected isOverdue(ticket: DashboardTicket): boolean {
    return isOverdueNow(ticket.dueAt, ticket.status, ticket.isOverdue, this.clock.now());
  }
}
