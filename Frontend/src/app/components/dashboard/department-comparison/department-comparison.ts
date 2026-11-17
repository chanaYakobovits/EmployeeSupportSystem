import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Params, RouterLink } from '@angular/router';

import { DashboardDepartmentComparison } from '../../../models/dashboard.models';
import { ListSort } from '../../../services/list-state';
import { Badge } from '../../shared/badge/badge';
import { DataTable, DataTableCell, DataTableColumn } from '../../shared/data-table/data-table';
import { formatDuration, formatNumber } from '../dashboard-format';
import { departmentDashboardUrl } from '../dashboard-urls';

export const COMPARISON_COLUMNS: readonly DataTableColumn[] = [
  { key: 'name', header: 'מחלקה מטפלת', sortKey: 'name' },
  { key: 'open', header: 'פתוחות', sortKey: 'open', firstDir: 'desc', align: 'end' },
  { key: 'overdue', header: 'חריגות SLA', sortKey: 'overdue', firstDir: 'desc', align: 'end' },
  { key: 'resolved', header: 'נפתרו בטווח', sortKey: 'resolved', firstDir: 'desc', align: 'end' },
  { key: 'average', header: 'זמן פתרון ממוצע', sortKey: 'average', firstDir: 'desc', align: 'end' },
];

/**
 * The executive's comparison between departments (spec 7.9) – by the handling department. The rows come sorted
 * (DashboardStore.comparisonRows); a header click asks for another order. A department's name opens its dashboard
 * with the same range. "לא נותבו" (no department) is not a link.
 */
@Component({
  selector: 'app-department-comparison',
  imports: [RouterLink, DataTable, DataTableCell, Badge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './department-comparison.html',
  styleUrl: './department-comparison.scss',
})
export class DepartmentComparison {
  readonly rows = input.required<readonly DashboardDepartmentComparison[]>();
  readonly sort = input.required<ListSort>();
  /** The range of the current dashboard – kept on the department's dashboard. */
  readonly rangeParams = input<Params>({});
  readonly sortChange = output<ListSort>();

  protected readonly columns = COMPARISON_COLUMNS;
  protected readonly number = formatNumber;
  protected readonly duration = formatDuration;
  protected readonly departmentUrl = departmentDashboardUrl;
  protected readonly trackBy = (row: DashboardDepartmentComparison) => row.departmentId ?? 'not-routed';
}
