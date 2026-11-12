import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';

import {
  RequesterStatusGroup,
  RequesterTicketListItem,
  RequesterTicketsSummary,
} from '../../../core/models/ticket.models';
import {
  DataTable,
  DataTableCell,
  DataTableColumn,
  DataTableRowCard,
} from '../../../shared/components/data-table/data-table';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { FilterBar } from '../../../shared/components/filter-bar/filter-bar';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { RelativeTime } from '../../../shared/components/relative-time/relative-time';
import { SearchField } from '../../../shared/components/search-field/search-field';
import { StatusBadge } from '../../../shared/components/status-badge/status-badge';
import { DateTimePipe } from '../../../shared/pipes/date-time.pipe';
import { NEW_TICKET_URL, ticketUrl } from '../employee-urls';
import { MyTicketsStore } from './my-tickets.store';

export type ViewFilter = RequesterStatusGroup | 'all';

interface SummaryCard {
  view: RequesterStatusGroup;
  label: string;
  icon: string;
  tone: 'primary' | 'warning' | 'success';
  value: (summary: RequesterTicketsSummary) => number;
}

/** "הפניות שלי" – the employee's home screen (spec 7.3). No urgency anywhere (spec 14, question 5). */
@Component({
  selector: 'app-my-tickets-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatIconModule,
    PageHeader,
    FilterBar,
    SearchField,
    DataTable,
    DataTableCell,
    DataTableRowCard,
    EmptyState,
    StatusBadge,
    RelativeTime,
    DateTimePipe,
  ],
  providers: [MyTicketsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './my-tickets-page.html',
  styleUrl: './my-tickets-page.scss',
})
export class MyTicketsPage {
  protected readonly store = inject(MyTicketsStore);
  private readonly router = inject(Router);

  protected readonly list = this.store.list;
  protected readonly state = this.list.state;
  protected readonly newTicketUrl = NEW_TICKET_URL;
  protected readonly ticketUrl = ticketUrl;

  /** Each card filters the list to its status group (the "closed" card shows every closed ticket, not only this month's). */
  protected readonly cards: readonly SummaryCard[] = [
    { view: 'open', label: 'פתוחות', icon: 'pending_actions', tone: 'primary', value: (s) => s.open },
    { view: 'waiting', label: 'ממתינות לתגובתי', icon: 'reply', tone: 'warning', value: (s) => s.waitingForMe },
    { view: 'closed', label: 'נסגרו החודש', icon: 'task_alt', tone: 'success', value: (s) => s.closedThisMonth },
  ];

  protected readonly columns: readonly DataTableColumn[] = [
    { key: 'id', header: "מס' פנייה" },
    { key: 'subject', header: 'נושא' },
    { key: 'category', header: 'קטגוריה' },
    { key: 'status', header: 'סטטוס', sortKey: 'status' },
    { key: 'handler', header: 'מטפל' },
    { key: 'createdAt', header: 'נפתחה', sortKey: 'createdAt' },
    { key: 'updatedAt', header: 'עודכנה', sortKey: 'updatedAt' },
  ];

  /** A ticket waiting for the employee's answer stands out (spec 7.3). */
  protected readonly rowClass = (ticket: RequesterTicketListItem): string | null =>
    ticket.status === 'WaitingForEmployee' ? 'row-attention' : null;

  protected viewFilter(): ViewFilter {
    return this.state().filters.view ?? 'all';
  }

  protected onSearch(text: string): void {
    this.list.setFilters({ search: text || null }, { replaceUrl: true });
  }

  protected onView(value: ViewFilter): void {
    this.list.setFilters({ view: value === 'all' ? null : value });
  }

  /** A card toggles its filter: a second click on the active card shows all tickets again. */
  protected onCard(view: RequesterStatusGroup): void {
    this.onView(this.state().filters.view === view ? 'all' : view);
  }

  protected openTicket(ticket: RequesterTicketListItem): void {
    void this.router.navigateByUrl(ticketUrl(ticket.id));
  }
}
