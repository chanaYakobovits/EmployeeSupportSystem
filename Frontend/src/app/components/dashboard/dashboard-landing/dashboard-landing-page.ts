import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { DashboardLandingStore } from '../../../services/dashboard-landing.store';
import { EmptyState } from '../../shared/empty-state/empty-state';
import { ErrorState } from '../../shared/error-state/error-state';
import { LoadingState } from '../../shared/loading-state/loading-state';
import { NO_MANAGED_DEPARTMENT_DESCRIPTION, NO_MANAGED_DEPARTMENT_HEADING } from '../dashboard-messages';

/**
 * /dashboard: moves on to the user's dashboard (replaceUrl, the range in the URL kept) – the organization for the
 * executive, the managed department for a department manager. A page and not a guard, so a failed lookup has an
 * error state with "נסה שוב".
 */
@Component({
  selector: 'app-dashboard-landing-page',
  imports: [LoadingState, ErrorState, EmptyState],
  providers: [DashboardLandingStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-landing-page.html',
  styleUrl: './dashboard-landing-page.scss',
})
export class DashboardLandingPage {
  protected readonly store = inject(DashboardLandingStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly noDepartmentHeading = NO_MANAGED_DEPARTMENT_HEADING;
  protected readonly noDepartmentDescription = NO_MANAGED_DEPARTMENT_DESCRIPTION;

  constructor() {
    this.go();
  }

  protected go(): void {
    this.store.resolve().subscribe((url) => {
      if (url !== null) {
        void this.router.navigate([url], { queryParams: this.route.snapshot.queryParams, replaceUrl: true });
      }
    });
  }
}
