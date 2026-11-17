import { HttpErrorResponse } from '@angular/common/http';
import { ANIMATION_MODULE_TYPE, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';

import { Role } from '../../../models/auth.models';
import { Department } from '../../../models/department.models';
import { AuthService } from '../../../services/auth.service';
import { DepartmentsApiService } from '../../../services/departments-api.service';
import { dashboardLandingUrl } from '../../../services/dashboard-landing.store';
import { MANAGER_ID, fakeDashboardAuth, fakeDepartment } from '../testing/dashboard-fixtures';
import { DashboardLandingPage } from './dashboard-landing-page';

@Component({ template: '' })
class Stub {}

describe('DashboardLandingPage', () => {
  let list: jasmine.Spy<() => Observable<Department[]>>;
  let harness: RouterTestingHarness;

  async function open(roles: Role[], url = '/dashboard'): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'dashboard', pathMatch: 'full', component: DashboardLandingPage },
          { path: 'dashboard/organization', component: Stub },
          { path: 'dashboard/departments/:id', component: Stub },
        ]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: DepartmentsApiService, useValue: { list } },
        { provide: AuthService, useValue: fakeDashboardAuth(roles) },
      ],
    });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    await harness.fixture.whenStable();
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  beforeEach(() => {
    list = jasmine.createSpy('list').and.returnValue(
      of([fakeDepartment({ id: 2, manager: null }), fakeDepartment({ id: 7 })]),
    );
  });

  it('the executive lands on the organization dashboard (no lookup), keeping the range', async () => {
    await open(['Employee', 'Executive', 'DepartmentManager'], '/dashboard?days=7');

    expect(TestBed.inject(Router).url).toBe('/dashboard/organization?days=7');
    expect(list).not.toHaveBeenCalled();
  });

  it('a department manager lands on the department they manage (not necessarily their own)', async () => {
    await open(['Employee', 'DepartmentManager']);

    expect(TestBed.inject(Router).url).toBe('/dashboard/departments/7');
  });

  it('a manager of no department → an explanation', async () => {
    list.and.returnValue(of([fakeDepartment({ manager: null })]));
    const element = await open(['Employee', 'DepartmentManager']);

    expect(TestBed.inject(Router).url).toBe('/dashboard');
    expect(element.textContent).toContain('לא נמצאה מחלקה בניהולך');
  });

  it('a failed lookup → an error with "נסה שוב"', async () => {
    list.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const element = await open(['Employee', 'DepartmentManager']);
    expect(element.querySelector('app-error-state')).not.toBeNull();

    list.and.returnValue(of([fakeDepartment({ id: 7 })]));
    element.querySelector<HTMLButtonElement>('app-error-state button')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/dashboard/departments/7');
  });
});

describe('dashboardLandingUrl', () => {
  it('by role', () => {
    const departments = [fakeDepartment({ id: 5 })];
    expect(dashboardLandingUrl(['Executive'], MANAGER_ID, departments)).toBe('/dashboard/organization');
    expect(dashboardLandingUrl(['DepartmentManager'], MANAGER_ID, departments)).toBe('/dashboard/departments/5');
    expect(dashboardLandingUrl(['DepartmentManager'], 999, departments)).toBeNull();
    expect(dashboardLandingUrl(['DepartmentManager'], null, departments)).toBeNull();
  });
});
