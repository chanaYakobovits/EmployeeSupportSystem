import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { DashboardStore } from '../../../services/dashboard.store';
import { PageHeader } from '../../shared/page-header/page-header';
import { DashboardBody } from '../dashboard-body/dashboard-body';
import { DashboardRangePicker } from '../dashboard-range-picker/dashboard-range-picker';
import { DepartmentComparison } from '../department-comparison/department-comparison';

/**
 * The organization's dashboard (spec 4.5, 7.9) – the executive's landing page: the same cards and charts for the whole
 * organization, the comparison between departments, and a selector to open a department's dashboard.
 */
@Component({
  selector: 'app-organization-dashboard-page',
  imports: [MatFormFieldModule, MatSelectModule, PageHeader, DashboardBody, DashboardRangePicker, DepartmentComparison],
  providers: [DashboardStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './organization-dashboard-page.html',
  styleUrl: './organization-dashboard-page.scss',
})
export class OrganizationDashboardPage {
  protected readonly store = inject(DashboardStore);
}
