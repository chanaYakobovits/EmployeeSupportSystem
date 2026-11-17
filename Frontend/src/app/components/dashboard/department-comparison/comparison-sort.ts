import { DashboardDepartmentComparison } from '../../../models/dashboard.models';
import { ListSort } from '../../../services/list-state';

/**
 * The order of the executive's comparison table. Sorted in the client – unlike every ticket list: the rows are the
 * server's grouped result (one per department, a few dozen at most), not records to page through.
 */
export const COMPARISON_SORT_FIELDS = ['name', 'open', 'overdue', 'resolved', 'average'] as const;
export type ComparisonSortField = (typeof COMPARISON_SORT_FIELDS)[number];

/** What needs attention first: the most overdue, then the most open. */
export const DEFAULT_COMPARISON_SORT: ListSort = { by: 'overdue', dir: 'desc' };

type Row = DashboardDepartmentComparison;

const NAME_COLLATOR = new Intl.Collator('he');

function name(row: Row): string {
  return row.departmentName ?? '';
}

function value(row: Row, by: ComparisonSortField): number | null {
  switch (by) {
    case 'open':
      return row.openCount;
    case 'overdue':
      return row.overdueOpenCount;
    case 'resolved':
      return row.resolvedInRangeCount;
    case 'average':
      return row.averageResolutionHours;
    default:
      return null;
  }
}

/**
 * Ties: overdue ↓, open ↓, name. A missing average is always last, and so is "לא נותבו" (no department – as the
 * server puts it: the remainder, so the rows add up to the cards). The input is not changed.
 */
export function sortComparison(rows: readonly Row[], sort: ListSort): Row[] {
  const by = (COMPARISON_SORT_FIELDS as readonly string[]).includes(sort.by)
    ? (sort.by as ComparisonSortField)
    : (DEFAULT_COMPARISON_SORT.by as ComparisonSortField);
  const sign = sort.dir === 'desc' ? -1 : 1;

  return [...rows].sort((a, b) => {
    const notRouted = Number(a.departmentId === null) - Number(b.departmentId === null);
    if (notRouted !== 0) {
      return notRouted;
    }
    let primary = 0;
    if (by === 'name') {
      primary = sign * NAME_COLLATOR.compare(name(a), name(b));
    } else {
      const va = value(a, by);
      const vb = value(b, by);
      if (va === null || vb === null) {
        primary = va === vb ? 0 : va === null ? 1 : -1;
      } else {
        primary = sign * (va - vb);
      }
    }
    return (
      primary ||
      b.overdueOpenCount - a.overdueOpenCount ||
      b.openCount - a.openCount ||
      NAME_COLLATOR.compare(name(a), name(b))
    );
  });
}
