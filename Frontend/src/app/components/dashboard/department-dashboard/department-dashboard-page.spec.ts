import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';

import { Role } from '../../../models/auth.models';
import { DepartmentDashboard } from '../../../models/dashboard.models';
import { Department } from '../../../models/department.models';
import { AuthService } from '../../../services/auth.service';
import { DashboardApiService } from '../../../services/dashboard-api.service';
import { DepartmentsApiService } from '../../../services/departments-api.service';
import { DASHBOARD_ROUTES } from '../dashboard.routes';
import { addDays, israelToday } from '../dashboard-range';
import {
  MANAGED_DEPARTMENT_ID,
  fakeDashboardAuth,
  fakeDepartment,
  fakeDepartmentDashboard,
} from '../testing/dashboard-fixtures';
import { DepartmentDashboardPage } from './department-dashboard-page';

@Component({ template: '' })
class Stub {}

describe('DepartmentDashboardPage', () => {
  let api: jasmine.SpyObj<DashboardApiService>;
  let departments: Department[];
  let harness: RouterTestingHarness;

  function configure(roles: Role[] = ['Employee', 'DepartmentManager']): void {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'handler/tickets/:id', component: Stub },
          { path: 'dashboard', children: DASHBOARD_ROUTES },
        ]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: DashboardApiService, useValue: api },
        { provide: DepartmentsApiService, useValue: { list: () => of(departments) } },
        { provide: AuthService, useValue: fakeDashboardAuth(roles) },
      ],
    });
  }

  beforeEach(() => {
    api = jasmine.createSpyObj<DashboardApiService>('DashboardApiService', ['department', 'organization']);
    api.department.and.returnValue(of(fakeDepartmentDashboard()));
    departments = [fakeDepartment()];
  });

  async function open(url: string, roles?: Role[]): Promise<HTMLElement> {
    configure(roles);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, DepartmentDashboardPage);
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  async function settle(): Promise<void> {
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  function tabs(element: HTMLElement): HTMLAnchorElement[] {
    return Array.from(element.querySelectorAll<HTMLAnchorElement>('a[mat-tab-link]'));
  }

  function toggle(element: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(element.querySelectorAll<HTMLButtonElement>('mat-button-toggle button')).find((b) =>
      b.textContent!.includes(text),
    )!;
  }

  it('opens the employees’ tickets with the default range: no view and no range is sent', async () => {
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);

    expect(api.department).toHaveBeenCalledOnceWith(MANAGED_DEPARTMENT_ID, { view: null });
    expect(element.querySelector('h1')!.textContent).toContain('דשבורד – הנהלת חשבונות');
    expect(element.querySelectorAll('.metric-card').length).toBe(6);
    expect(element.querySelectorAll('app-chart-card').length).toBe(3);
  });

  it('the "בטיפול המחלקה" tab → view=handling in the URL and Handling in the API; the range is kept', async () => {
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}?days=7`);
    const [requesters, handling] = tabs(element);
    expect(requesters.textContent).toContain('פניות של עובדי המחלקה');
    expect(requesters.classList).toContain('mdc-tab--active');

    handling.click();
    await settle();

    expect(TestBed.inject(Router).url).toBe(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}?days=7&view=handling`);
    expect(api.department).toHaveBeenCalledWith(MANAGED_DEPARTMENT_ID, jasmine.objectContaining({ view: 'Handling' }));
    expect(tabs(element)[1].classList).toContain('mdc-tab--active');
  });

  it('a department without handlers has no "בטיפול המחלקה" tab – unless the URL asks for it', async () => {
    departments = [fakeDepartment({ handlerCount: 0 })];
    let element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    expect(tabs(element).length).toBe(0);
    expect(element.querySelector('.single-view')!.textContent).toContain('פניות של עובדי המחלקה');

    TestBed.resetTestingModule();
    element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}?view=handling`);
    expect(tabs(element).length).toBe(2);
  });

  it('a range → the URL and the API: 7 days up to today in Israel, back to 30 removes it', async () => {
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    const router = TestBed.inject(Router);

    toggle(element, '7 יום').click();
    await settle();

    const today = israelToday();
    expect(router.url).toBe(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}?days=7`);
    expect(api.department).toHaveBeenCalledWith(MANAGED_DEPARTMENT_ID, { from: addDays(today, -6), to: today, view: null });

    toggle(element, '30 יום').click();
    await settle();
    expect(router.url).toBe(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
  });

  it('a custom range: from/to in the URL and the API; an invalid one is not sent', async () => {
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    toggle(element, 'טווח מותאם').click();
    await settle();

    const [from, to] = Array.from(element.querySelectorAll<HTMLInputElement>('input[type="date"]'));
    from.value = '2026-03-10';
    from.dispatchEvent(new Event('change'));
    to.value = '2026-03-01';
    to.dispatchEvent(new Event('change'));
    element.querySelector<HTMLButtonElement>('.apply-button')!.click();
    await settle();

    expect(element.querySelector('.range-error')!.textContent).toContain('תאריך ההתחלה חייב להיות לפני');
    expect(api.department).toHaveBeenCalledTimes(1);

    to.value = '2026-03-31';
    to.dispatchEvent(new Event('change'));
    element.querySelector<HTMLButtonElement>('.apply-button')!.click();
    await settle();

    expect(TestBed.inject(Router).url).toBe(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}?from=2026-03-10&to=2026-03-31`);
    expect(api.department).toHaveBeenCalledWith(MANAGED_DEPARTMENT_ID, { from: '2026-03-10', to: '2026-03-31', view: null });
  });

  it('the overdue card says "דורש טיפול" with an icon (not colour alone); every card has its explanation', async () => {
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    const overdue = element.querySelector('[data-metric="overdue"]')!;

    expect(overdue.classList).toContain('attention');
    expect(overdue.querySelector('.attention-tag')!.textContent).toContain('דורש טיפול');
    expect(element.querySelector('[data-metric="averageResolution"] .metric-value')!.textContent).toContain('יום ו-4 שעות');
    for (const card of Array.from(element.querySelectorAll('.metric-card'))) {
      const label = card.querySelector('.metric-label')!.textContent!.trim();
      expect(card.querySelector('.metric-hint')!.textContent!.trim().length).toBeGreaterThan(10);
      expect(card.querySelector('.metric-info')!.getAttribute('aria-label')).toBe(`הסבר: ${label}`);
    }
  });

  it('no overdue → no highlight', async () => {
    api.department.and.returnValue(of(fakeDepartmentDashboard({}, { overdueOpenCount: 0 })));
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);

    expect(element.querySelector('[data-metric="overdue"]')!.classList).not.toContain('attention');
    expect(element.querySelector('.attention-tag')).toBeNull();
  });

  it('"הצג כטבלה" shows the chart’s data as a table, and back', async () => {
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    const card = element.querySelectorAll('app-chart-card')[0];
    expect(card.querySelector('canvas')!.getAttribute('aria-label')).toContain('שכר ותלושים – 9');

    card.querySelector<HTMLButtonElement>('.chart-toggle')!.click();
    await settle();

    const rows = Array.from(card.querySelectorAll('tbody tr')).map((row) =>
      Array.from(row.children).map((cell) => cell.textContent!.trim()),
    );
    expect(rows).toEqual([
      ['שכר ותלושים', '9'],
      ['תקלת מחשב', '4'],
      ['טרם סווגה', '1'],
    ]);
    expect(card.querySelector('canvas')).toBeNull();
    expect(card.querySelector('.chart-toggle')!.textContent).toContain('הצג כגרף');
  });

  it('the oldest tickets link to the handling screen inside the dashboard, overdue marked in text', async () => {
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    const link = element.querySelector<HTMLAnchorElement>('app-oldest-tickets a.ticket-number')!;

    expect(link.getAttribute('href')).toBe('/dashboard/tickets/1042');
    expect(element.querySelector('app-oldest-tickets tr.row-overdue')!.textContent).toContain('חריגה');
    // The employees' tickets: the handling department is the useful one.
    expect(element.querySelector('app-oldest-tickets thead')!.textContent).toContain('מחלקה מטפלת');
    expect(element.querySelector('app-oldest-tickets thead')!.textContent).not.toContain('מחלקת הפונה');
  });

  it('nothing at all in the range → "אין פניות בטווח שנבחר" instead of the numbers', async () => {
    api.department.and.returnValue(
      of(fakeDepartmentDashboard({ oldestOpen: [] }, { openCount: 0, overdueOpenCount: 0, openedInRangeCount: 0, resolvedInRangeCount: 0 })),
    );
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);

    expect(element.querySelector('app-empty-state')!.textContent).toContain('אין פניות בטווח שנבחר');
    expect(element.querySelector('app-metric-cards')).toBeNull();
    expect(element.querySelector('app-chart-card')).toBeNull();
  });

  it('nothing opened in the range but open tickets → the cards and the table stay, the charts say the range is empty', async () => {
    api.department.and.returnValue(of(fakeDepartmentDashboard({}, { openedInRangeCount: 0, resolvedInRangeCount: 0 })));
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);

    expect(element.querySelector('app-metric-cards')).not.toBeNull();
    expect(element.querySelector('app-oldest-tickets')).not.toBeNull();
    expect(element.querySelector('app-chart-card')).toBeNull();
    expect(element.querySelector('.range-empty')!.textContent).toContain('אין פניות בטווח שנבחר');
  });

  it('an error → "נסה שוב" asks again; refresh asks again and keeps the numbers', async () => {
    api.department.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    expect(element.querySelector('app-error-state')).not.toBeNull();

    api.department.and.returnValue(of(fakeDepartmentDashboard()));
    element.querySelector<HTMLButtonElement>('app-error-state button')!.click();
    await settle();
    expect(api.department).toHaveBeenCalledTimes(2);
    expect(element.querySelector('app-metric-cards')).not.toBeNull();

    const pending = new Observable<DepartmentDashboard>(() => undefined);
    api.department.and.returnValue(pending);
    element.querySelector<HTMLButtonElement>('.refresh-button')!.click();
    await settle();
    expect(api.department).toHaveBeenCalledTimes(3);
    expect(element.querySelector('app-metric-cards')).not.toBeNull();
    expect(element.querySelector('mat-progress-bar')).not.toBeNull();
  });

  it('404 (not the manager’s department) → "המחלקה לא נמצאה"', async () => {
    api.department.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    const element = await open('/dashboard/departments/9');

    expect(element.querySelector('app-empty-state')!.textContent).toContain('המחלקה לא נמצאה');
  });

  it('the executive: back to the organization and a department selector; a manager has neither', async () => {
    let element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}?days=90`, ['Employee', 'Executive']);
    expect(element.querySelector<HTMLAnchorElement>('.back-link')!.getAttribute('href')).toBe('/dashboard/organization?days=90');
    expect(element.querySelector('mat-select')).not.toBeNull();

    TestBed.resetTestingModule();
    element = await open(`/dashboard/departments/${MANAGED_DEPARTMENT_ID}`);
    expect(element.querySelector('.back-link')).toBeNull();
    expect(element.querySelector('mat-select')).toBeNull();
  });
});
