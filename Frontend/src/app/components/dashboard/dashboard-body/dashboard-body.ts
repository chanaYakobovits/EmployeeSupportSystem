import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { DashboardStore } from '../../../services/dashboard.store';
import { EmptyState } from '../../shared/empty-state/empty-state';
import { ErrorState } from '../../shared/error-state/error-state';
import { LoadingState } from '../../shared/loading-state/loading-state';
import { RelativeTime } from '../../shared/relative-time/relative-time';
import { ChartCard } from '../chart-card/chart-card';
import { categoryChart, statusChart, trendChart } from '../dashboard-charts';
import { EMPTY_RANGE_DESCRIPTION, EMPTY_RANGE_HEADING } from '../dashboard-messages';
import { MetricCards } from '../metric-cards/metric-cards';
import { OldestTickets, OldestTicketsDepartment } from '../oldest-tickets/oldest-tickets';

/**
 * What every dashboard shows (spec 7.9): "עודכן לאחרונה" + refresh, the cards, the three charts and the oldest open
 * tickets – in the four states of 7.10. Extra sections of a page (the executive's comparison) are projected after them.
 * Reads the page's DashboardStore.
 */
@Component({
  selector: 'app-dashboard-body',
  imports: [
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    LoadingState,
    ErrorState,
    EmptyState,
    RelativeTime,
    MetricCards,
    ChartCard,
    OldestTickets,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-body.html',
  styleUrl: './dashboard-body.scss',
})
export class DashboardBody {
  readonly oldestDepartment = input<OldestTicketsDepartment>('both');

  protected readonly store = inject(DashboardStore);
  protected readonly emptyHeading = EMPTY_RANGE_HEADING;
  protected readonly emptyDescription = EMPTY_RANGE_DESCRIPTION;

  protected readonly charts = computed(() => {
    const metrics = this.store.metrics();
    return metrics ? [categoryChart(metrics.byCategory), statusChart(metrics.byStatus), trendChart(metrics.trend)] : [];
  });
}
