import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';

import { DashboardView } from '../../../models/dashboard.models';
import { DashboardStore } from '../../../services/dashboard.store';
import { EmptyState } from '../../shared/empty-state/empty-state';
import { PageHeader } from '../../shared/page-header/page-header';
import { DashboardBody } from '../dashboard-body/dashboard-body';
import { DEPARTMENT_NOT_FOUND_DESCRIPTION, DEPARTMENT_NOT_FOUND_HEADING, VIEW_LABELS } from '../dashboard-messages';
import { DashboardRangePicker } from '../dashboard-range-picker/dashboard-range-picker';
import { ORGANIZATION_DASHBOARD_URL } from '../dashboard-urls';
import { OldestTicketsDepartment } from '../oldest-tickets/oldest-tickets';

/**
 * A department's dashboard (spec 4.5, 7.9): two tabs – the tickets its employees opened and (when it handles tickets)
 * the tickets it handles – and a date range, all in the URL. A department manager lands on their department; the
 * executive reaches any department from the organization dashboard or the selector here.
 */
@Component({
  selector: 'app-department-dashboard-page',
  imports: [
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    MatTabsModule,
    PageHeader,
    EmptyState,
    DashboardBody,
    DashboardRangePicker,
  ],
  providers: [DashboardStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './department-dashboard-page.html',
  styleUrl: './department-dashboard-page.scss',
})
export class DepartmentDashboardPage {
  protected readonly store = inject(DashboardStore);

  protected readonly organizationUrl = ORGANIZATION_DASHBOARD_URL;
  protected readonly notFoundHeading = DEPARTMENT_NOT_FOUND_HEADING;
  protected readonly notFoundDescription = DEPARTMENT_NOT_FOUND_DESCRIPTION;

  protected readonly views: readonly { view: DashboardView; label: string }[] = [
    { view: 'Requesters', label: VIEW_LABELS.Requesters },
    { view: 'Handling', label: VIEW_LABELS.Handling },
  ];

  protected readonly visibleViews = computed(() =>
    this.views.filter((tab) => tab.view === 'Requesters' || this.store.showHandlingTab()),
  );

  /** From the response, or from the departments list while it loads. */
  protected readonly departmentName = computed(() => {
    const id = this.store.departmentId();
    return (
      this.store.departmentDashboard()?.department.name ?? this.store.departments().find((d) => d.id === id)?.name ?? null
    );
  });

  protected readonly heading = computed(() => {
    const name = this.departmentName();
    return name ? `דשבורד – ${name}` : 'דשבורד מחלקתי';
  });

  /** The employees' tickets: who handles them; the handled tickets: whose they are. */
  protected readonly oldestDepartment = computed<OldestTicketsDepartment>(() =>
    this.store.view() === 'Handling' ? 'requester' : 'assigned',
  );

  protected onDepartment(id: number): void {
    if (id !== this.store.departmentId()) {
      this.store.openDepartment(id);
    }
  }
}
