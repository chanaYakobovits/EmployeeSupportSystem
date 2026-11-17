import { TICKET_STATUS_VALUES } from '../../models/ticket.models';
import { statusLabel } from '../../pipes/status-label.pipe';
import { TICKET_STATUS_COLOR_VARS, cssColor } from '../../services/status-colors';
import { categoryChart, statusChart, trendChart } from './dashboard-charts';
import { fakeDepartmentDashboard } from './testing/dashboard-fixtures';

describe('dashboard-charts', () => {
  const data = fakeDepartmentDashboard();
  /** Every variable resolves to its own name – shows which token each bar uses. */
  const byName = { color: (name: string) => `color(${name})`, reducedMotion: true };

  it('status chart: the colours are the status tokens of the shared source (as the status badge)', () => {
    const chart = statusChart(data.byStatus, byName);

    expect(chart.config.data.datasets[0].backgroundColor).toEqual(
      TICKET_STATUS_VALUES.map((status) => `color(${TICKET_STATUS_COLOR_VARS[status]})`),
    );
    expect(chart.config.data.labels).toEqual(TICKET_STATUS_VALUES.map((status) => statusLabel(status)));
  });

  it('status chart with the real resolver: the values of styles.scss', () => {
    const chart = statusChart(data.byStatus, { reducedMotion: true });
    const colors = chart.config.data.datasets[0].backgroundColor as string[];

    expect(colors[0]).toBe(cssColor('--status-new'));
    expect(colors[0]).not.toBe('');
  });

  it('category chart: horizontal, labels on the right, the largest first; null → "טרם סווגה"', () => {
    const chart = categoryChart(data.byCategory, byName);

    expect(chart.config.options?.indexAxis).toBe('y');
    expect(chart.config.options?.scales?.['y']?.position).toBe('right');
    expect(chart.config.data.labels).toEqual(['שכר ותלושים', 'תקלת מחשב', 'טרם סווגה']);
    expect(chart.config.data.datasets[0].data).toEqual([9, 4, 1]);
  });

  it('category chart: "טרם סווגה" stays last even when it is the largest (as the server)', () => {
    const chart = categoryChart(
      [
        { categoryId: null, categoryName: null, count: 50 },
        { categoryId: 1, categoryName: 'תקלת מחשב', count: 4 },
      ],
      byName,
    );
    expect(chart.config.data.labels).toEqual(['תקלת מחשב', 'טרם סווגה']);
  });

  it('trend: time left to right, the legend and the tooltip right to left, the lines differ beyond colour', () => {
    const chart = trendChart(data.trend, byName);
    const options = chart.config.options!;

    expect(options.scales?.['x']?.reverse).toBeFalse();
    expect(options.plugins?.legend?.rtl).toBeTrue();
    expect(options.plugins?.tooltip?.rtl).toBeTrue();
    expect(chart.config.data.labels).toEqual(['15/11', '16/11', '17/11']);
    const [opened, closed] = chart.config.data.datasets;
    expect(opened.label).toBe('נפתחו');
    expect(closed.label).toBe('נסגרו');
    expect(closed.borderDash).toEqual([6, 4]);
    expect(closed.pointStyle).not.toBe(opened.pointStyle);
  });

  it('every chart’s table has the same numbers as the chart, row by row', () => {
    for (const chart of [categoryChart(data.byCategory, byName), statusChart(data.byStatus, byName)]) {
      const labels = chart.config.data.labels as string[];
      const values = chart.config.data.datasets[0].data as number[];
      expect(chart.table.rows).toEqual(labels.map((label, i) => [label, String(values[i])]));
    }

    const trend = trendChart(data.trend, byName);
    expect(trend.table.columns).toEqual(['תאריך', 'נפתחו', 'נסגרו']);
    expect(trend.table.rows).toEqual([
      ['15/11/2026', '3', '1'],
      ['16/11/2026', '5', '2'],
      ['17/11/2026', '0', '4'],
    ]);
  });

  it('the summaries (alternative text) say what the charts show', () => {
    expect(categoryChart(data.byCategory, byName).summary).toContain('שכר ותלושים – 9');
    expect(statusChart(data.byStatus, byName).summary).toContain('חדשה – 1');
    expect(trendChart(data.trend, byName).summary).toBe(
      'מגמה – נפתחו מול נסגרו מ-15/11/2026 עד 17/11/2026: נפתחו 8, נסגרו סופית 7. היום עם הכי הרבה פניות שנפתחו: 16/11/2026 (5).',
    );
  });

  it('reduced motion → no animation', () => {
    expect(trendChart(data.trend, byName).config.options?.animation).toBeFalse();
  });
});
