import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';

import { AuthService } from '../../../services/auth.service';
import { DashboardApiService } from '../../../services/dashboard-api.service';
import { DepartmentsApiService } from '../../../services/departments-api.service';
import { DASHBOARD_ROUTES } from '../dashboard.routes';
import { DEFAULT_COMPARISON_SORT, sortComparison } from '../department-comparison/comparison-sort';
import {
  fakeDashboardAuth,
  fakeDepartment,
  fakeDepartmentDashboard,
  fakeOrganizationDashboard,
} from '../testing/dashboard-fixtures';
import { OrganizationDashboardPage } from './organization-dashboard-page';

@Component({ template: '' })
class Stub {}

describe('OrganizationDashboardPage', () => {
  let api: jasmine.SpyObj<DashboardApiService>;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    api = jasmine.createSpyObj<DashboardApiService>('DashboardApiService', ['department', 'organization']);
    api.organization.and.returnValue(of(fakeOrganizationDashboard()));
    api.department.and.returnValue(of(fakeDepartmentDashboard()));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'handler/tickets/:id', component: Stub },
          { path: 'dashboard', children: DASHBOARD_ROUTES },
        ]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: DashboardApiService, useValue: api },
        {
          provide: DepartmentsApiService,
          useValue: { list: () => of([fakeDepartment({ id: 2, name: 'מחשוב' }), fakeDepartment({ id: 3, name: 'משאבי אנוש' })]) },
        },
        { provide: AuthService, useValue: fakeDashboardAuth(['Employee', 'Executive']) },
      ],
    });
  });

  async function open(url = '/dashboard/organization'): Promise<HTMLElement> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, OrganizationDashboardPage);
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  function comparisonNames(element: HTMLElement): string[] {
    return Array.from(element.querySelectorAll('app-department-comparison tbody tr')).map(
      (row) => row.querySelector('td')!.textContent!.replace('לא פעילה', '').trim(),
    );
  }

  it('the same cards and charts for the whole organization', async () => {
    const element = await open();

    expect(api.organization).toHaveBeenCalledOnceWith({});
    expect(element.querySelector('h1')!.textContent).toContain('דשבורד ארגוני');
    expect(element.querySelectorAll('.metric-card').length).toBe(6);
    expect(element.querySelectorAll('app-chart-card').length).toBe(3);
    // The organization: both departments of every ticket.
    const head = element.querySelector('app-oldest-tickets thead')!.textContent!;
    expect(head).toContain('מחלקת הפונה');
    expect(head).toContain('מחלקה מטפלת');
  });

  it('the comparison: overdue first, then open; a header sorts by its column', async () => {
    const element = await open();

    expect(comparisonNames(element)).toEqual(['משאבי אנוש', 'מחשוב', 'הנהלת חשבונות', 'לא נותבו']);
    expect(element.querySelector('app-department-comparison')!.textContent).toContain('לא פעילה');

    const nameHeader = Array.from(element.querySelectorAll<HTMLButtonElement>('app-department-comparison .sort-button')).find(
      (button) => button.textContent!.includes('מחלקה מטפלת'),
    )!;
    nameHeader.click();
    harness.detectChanges();

    // "לא נותבו" (no department) stays last in every order.
    expect(comparisonNames(element)).toEqual(['הנהלת חשבונות', 'מחשוב', 'משאבי אנוש', 'לא נותבו']);
  });

  it('a department in the comparison → its dashboard, with the same range; "לא נותבו" is not a link', async () => {
    const element = await open('/dashboard/organization?days=7');
    const links = Array.from(element.querySelectorAll<HTMLAnchorElement>('app-department-comparison a.department-link'));
    expect(links.map((link) => link.textContent!.trim())).toEqual(['משאבי אנוש', 'מחשוב', 'הנהלת חשבונות']);

    links[0].click();
    await harness.fixture.whenStable();
    // The department page renders and asks for its numbers.
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/dashboard/departments/3?days=7');
    expect(api.department).toHaveBeenCalledWith(3, jasmine.objectContaining({ view: null }));
  });

  it('the selector opens a department’s dashboard', async () => {
    await open('/dashboard/organization?from=2026-01-01&to=2026-01-31');
    const page = harness.routeDebugElement!.componentInstance as OrganizationDashboardPage;

    (page as unknown as { store: { openDepartment(id: number): void } }).store.openDepartment(2);
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/dashboard/departments/2?from=2026-01-01&to=2026-01-31');
  });
});

describe('sortComparison', () => {
  const rows = fakeOrganizationDashboard().departments;

  it('a missing average is last in both directions; the input is not changed', () => {
    const asc = sortComparison(rows, { by: 'average', dir: 'asc' }).map((r) => r.departmentName);
    const desc = sortComparison(rows, { by: 'average', dir: 'desc' }).map((r) => r.departmentName);

    expect(asc).toEqual(['מחשוב', 'משאבי אנוש', 'הנהלת חשבונות', 'לא נותבו']);
    expect(desc).toEqual(['משאבי אנוש', 'מחשוב', 'הנהלת חשבונות', 'לא נותבו']);
    expect(rows[0].departmentName).toBe('מחשוב');
  });

  it('"לא נותבו" is last also when it would sort first', () => {
    const names = sortComparison(rows, { by: 'name', dir: 'desc' }).map((r) => r.departmentName);
    expect(names).toEqual(['משאבי אנוש', 'מחשוב', 'הנהלת חשבונות', 'לא נותבו']);
  });

  it('an unknown field → by the default field', () => {
    expect(sortComparison(rows, { by: 'x', dir: 'desc' })).toEqual(sortComparison(rows, DEFAULT_COMPARISON_SORT));
  });
});
