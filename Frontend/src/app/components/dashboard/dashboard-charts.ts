import { ChartConfiguration, ChartType } from 'chart.js';

import {
  DashboardCategoryCount,
  DashboardStatusCount,
  DashboardTrendPoint,
} from '../../models/dashboard.models';
import { statusLabel } from '../../pipes/status-label.pipe';
import { TICKET_STATUS_COLOR_VARS, cssColor } from '../../services/status-colors';
import { formatDay, formatDayMonth, formatNumber } from './dashboard-format';
import { CHART_NOTES, UNCLASSIFIED_CATEGORY } from './dashboard-messages';

/**
 * The three charts of a dashboard (spec 7.9). Pure: every builder returns the chart's configuration, the same data as
 * a table ("הצג כטבלה") and a text summary for the canvas's aria-label (spec 11.6) – all from one array, so the three
 * can never disagree. Colours are CSS variables, resolved when the chart is built (a canvas cannot read var()).
 */

/** The table alternative of a chart: the first column is the label, the others the values (formatted). */
export interface ChartTable {
  columns: readonly string[];
  rows: readonly (readonly string[])[];
}

export interface DashboardChart<T extends ChartType = ChartType> {
  id: string;
  heading: string;
  /** What the chart counts – a line under the heading. */
  note: string;
  config: ChartConfiguration<T>;
  table: ChartTable;
  /** What the chart shows, as text: the canvas's alternative text. */
  summary: string;
}

/** Resolves a CSS variable to a colour; injectable for the tests. */
export type ColorResolver = (varName: string) => string;

export interface ChartBuildOptions {
  color?: ColorResolver;
  /** prefers-reduced-motion: no animation. */
  reducedMotion?: boolean;
}

function prefersReducedMotion(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}

function common(options: ChartBuildOptions) {
  const color = options.color ?? cssColor;
  const reduced = options.reducedMotion ?? prefersReducedMotion();
  const text = color('--text-secondary');
  const grid = color('--border');
  return { color, text, grid, animation: reduced ? (false as const) : { duration: 250 } };
}

/** Legend and tooltip right-to-left (Hebrew); the axes keep their own direction. */
function rtlPlugins(showLegend: boolean) {
  return {
    legend: {
      display: showLegend,
      rtl: true,
      textDirection: 'rtl',
      position: 'top' as const,
      align: 'start' as const,
      labels: { usePointStyle: true },
    },
    tooltip: { rtl: true, textDirection: 'rtl' },
  };
}

/** "חומרה – 12, תוכנה – 8". */
function list(pairs: readonly (readonly [string, number])[]): string {
  return pairs.map(([label, count]) => `${label} – ${formatNumber(count)}`).join(', ');
}

/** Opened in the range by category: horizontal bars (long Hebrew labels), the largest first; "טרם סווגה" last (as the server). */
export function categoryChart(
  items: readonly DashboardCategoryCount[],
  options: ChartBuildOptions = {},
): DashboardChart<'bar'> {
  const { color, text, grid, animation } = common(options);
  const sorted = [...items]
    .map((item) => ({ label: item.categoryName ?? UNCLASSIFIED_CATEGORY, count: item.count, unclassified: item.categoryId === null }))
    .sort(
      (a, b) =>
        Number(a.unclassified) - Number(b.unclassified) || b.count - a.count || a.label.localeCompare(b.label, 'he'),
    );
  const heading = 'פניות לפי קטגוריה';

  return {
    id: 'by-category',
    heading,
    note: CHART_NOTES.opened,
    config: {
      type: 'bar',
      data: {
        labels: sorted.map((item) => item.label),
        datasets: [
          {
            label: 'פניות שנפתחו',
            data: sorted.map((item) => item.count),
            backgroundColor: color('--primary'),
            borderRadius: 6,
            maxBarThickness: 28,
          },
        ],
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        animation,
        plugins: rtlPlugins(false),
        scales: {
          // RTL: the labels on the right, the bars grow to the left.
          x: { reverse: true, beginAtZero: true, ticks: { precision: 0, color: text }, grid: { color: grid } },
          y: { position: 'right', ticks: { color: text }, grid: { display: false } },
        },
      },
    },
    table: { columns: ['קטגוריה', 'פניות'], rows: sorted.map((item) => [item.label, formatNumber(item.count)]) },
    summary: `${heading} (פניות שנפתחו בטווח): ${list(sorted.map((item) => [item.label, item.count] as const))}.`,
  };
}

/** Opened in the range by their current status: bars in the status colours (TICKET_STATUS_COLOR_VARS). */
export function statusChart(items: readonly DashboardStatusCount[], options: ChartBuildOptions = {}): DashboardChart<'bar'> {
  const { color, text, grid, animation } = common(options);
  const labels = items.map((item) => statusLabel(item.status));
  const heading = 'פניות לפי סטטוס';

  return {
    id: 'by-status',
    heading,
    note: CHART_NOTES.status,
    config: {
      type: 'bar',
      data: {
        labels,
        datasets: [
          {
            label: 'פניות שנפתחו',
            data: items.map((item) => item.count),
            backgroundColor: items.map((item) => color(TICKET_STATUS_COLOR_VARS[item.status])),
            borderRadius: 6,
            maxBarThickness: 40,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation,
        plugins: rtlPlugins(false),
        scales: {
          // RTL reading order: the first status (חדשה) on the right.
          x: { reverse: true, ticks: { color: text }, grid: { display: false } },
          y: { position: 'right', beginAtZero: true, ticks: { precision: 0, color: text }, grid: { color: grid } },
        },
      },
    },
    table: {
      columns: ['סטטוס', 'פניות'],
      rows: items.map((item, i) => [labels[i], formatNumber(item.count)]),
    },
    summary: `${heading} (פניות שנפתחו בטווח, לפי הסטטוס הנוכחי): ${list(items.map((item, i) => [labels[i], item.count] as const))}.`,
  };
}

/**
 * Opened vs. closed per day: two lines. Time runs left to right (as every time axis); the legend and the tooltip are
 * right to left. The lines differ by more than colour: point shape and a dashed line for "נסגרו".
 */
export function trendChart(points: readonly DashboardTrendPoint[], options: ChartBuildOptions = {}): DashboardChart<'line'> {
  const { color, text, grid, animation } = common(options);
  const opened = color('--primary');
  const closed = color('--success-text');
  const heading = 'מגמה – נפתחו מול נסגרו';
  const totalOpened = points.reduce((sum, p) => sum + p.opened, 0);
  const totalClosed = points.reduce((sum, p) => sum + p.closed, 0);
  const peak = points.reduce<DashboardTrendPoint | null>((best, p) => (best === null || p.opened > best.opened ? p : best), null);
  const period = points.length > 0 ? ` מ-${formatDay(points[0].date)} עד ${formatDay(points[points.length - 1].date)}` : '';
  const peakText = peak && peak.opened > 0 ? ` היום עם הכי הרבה פניות שנפתחו: ${formatDay(peak.date)} (${formatNumber(peak.opened)}).` : '';

  return {
    id: 'trend',
    heading,
    note: CHART_NOTES.trend,
    config: {
      type: 'line',
      data: {
        labels: points.map((p) => formatDayMonth(p.date)),
        datasets: [
          {
            label: 'נפתחו',
            data: points.map((p) => p.opened),
            borderColor: opened,
            backgroundColor: opened,
            pointStyle: 'circle',
            pointRadius: 3,
            tension: 0.25,
          },
          {
            label: 'נסגרו',
            data: points.map((p) => p.closed),
            borderColor: closed,
            backgroundColor: closed,
            borderDash: [6, 4],
            pointStyle: 'rect',
            pointRadius: 3,
            tension: 0.25,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation,
        interaction: { mode: 'index', intersect: false },
        plugins: rtlPlugins(true),
        scales: {
          x: { reverse: false, ticks: { color: text, autoSkip: true, maxRotation: 0 }, grid: { display: false } },
          y: { beginAtZero: true, ticks: { precision: 0, color: text }, grid: { color: grid } },
        },
      },
    },
    table: {
      columns: ['תאריך', 'נפתחו', 'נסגרו'],
      rows: points.map((p) => [formatDay(p.date), formatNumber(p.opened), formatNumber(p.closed)]),
    },
    summary: `${heading}${period}: נפתחו ${formatNumber(totalOpened)}, נסגרו סופית ${formatNumber(totalClosed)}.${peakText}`,
  };
}
