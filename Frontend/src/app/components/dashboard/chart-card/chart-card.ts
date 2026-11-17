import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { BaseChartDirective } from 'ng2-charts';

import { DashboardChart } from '../dashboard-charts';

/**
 * One chart of a dashboard in a card: a heading and "הצג כטבלה" – the same data as a table (spec 11.6: an alternative
 * to every chart). The canvas is an image with the chart's text summary as its name.
 * Chart.js is registered by the dashboard area's routes (provideCharts) – only what the charts use.
 */
@Component({
  selector: 'app-chart-card',
  imports: [BaseChartDirective, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './chart-card.html',
  styleUrl: './chart-card.scss',
})
export class ChartCard {
  readonly chart = input.required<DashboardChart>();

  protected readonly showTable = signal(false);
  protected readonly headingId = computed(() => `chart-${this.chart().id}-heading`);
  protected readonly contentId = computed(() => `chart-${this.chart().id}-content`);

  /** Horizontal bars grow with the number of labels; the others have a fixed height. */
  protected readonly height = computed(() => {
    const config = this.chart().config;
    const horizontal = (config.options as { indexAxis?: string } | undefined)?.indexAxis === 'y';
    return horizontal ? Math.max(240, (config.data.labels?.length ?? 0) * 34 + 48) : 280;
  });

  protected toggle(): void {
    this.showTable.update((value) => !value);
  }
}
