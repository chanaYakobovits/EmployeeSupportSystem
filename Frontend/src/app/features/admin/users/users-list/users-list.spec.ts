import { ANIMATION_MODULE_TYPE } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';

import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { PagedResult } from '../../../../core/models/paging.models';
import { UserListItem } from '../../../../core/models/user.models';
import { fakeUserDetails } from '../../testing/admin-fixtures';
import { UsersList } from './users-list';

describe('UsersList', () => {
  let usersApi: jasmine.SpyObj<UsersApiService>;

  beforeEach(() => {
    usersApi = jasmine.createSpyObj<UsersApiService>('UsersApiService', ['list']);
    const departmentsApi = jasmine.createSpyObj<DepartmentsApiService>('DepartmentsApiService', ['list']);
    departmentsApi.list.and.returnValue(of([]));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'admin/users', component: UsersList }]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: UsersApiService, useValue: usersApi },
        { provide: DepartmentsApiService, useValue: departmentsApi },
      ],
    });
  });

  function result(items: UserListItem[]): PagedResult<UserListItem> {
    return { items, page: 1, pageSize: 25, totalCount: items.length };
  }

  async function open(url: string): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, UsersList);
    harness.detectChanges();
    return harness.routeNativeElement!;
  }

  it('sends the filters from the URL to the server', async () => {
    usersApi.list.and.returnValue(of(result([])));
    await open('/admin/users?search=דנה&isActive=false&role=Handler&departmentId=3&sortBy=email&sortDir=desc&page=2');
    expect(usersApi.list).toHaveBeenCalledWith(
      jasmine.objectContaining({
        search: 'דנה',
        isActive: false,
        role: 'Handler',
        departmentId: 3,
        sortBy: 'email',
        sortDir: 'desc',
        page: 2,
      }),
    );
  });

  it('no users and no filter: "אין עובדים" with a "new employee" action', async () => {
    usersApi.list.and.returnValue(of(result([])));
    const element = await open('/admin/users');
    expect(element.textContent).toContain('אין עובדים');
    expect(element.textContent).not.toContain('לא נמצאו עובדים');
  });

  it('no results for a filter: "לא נמצאו עובדים" with "clear filters"', async () => {
    usersApi.list.and.returnValue(of(result([])));
    const element = await open('/admin/users?search=zzz');
    expect(element.textContent).toContain('לא נמצאו עובדים');
    expect(element.textContent).not.toContain('אין עובדים');
    expect(element.textContent).toContain('ניקוי סינון');
  });

  it('shows the columns, system roles in Hebrew and the active tag', async () => {
    usersApi.list.and.returnValue(
      of(result([fakeUserDetails({ roles: ['Employee', 'Handler', 'Admin'], isActive: false })])),
    );
    const element = await open('/admin/users');
    const row = element.querySelector('tbody tr')!;
    expect(row.textContent).toContain('EMP-00042');
    expect(row.textContent).toContain('יוסי כהן');
    expect(row.textContent).toContain('yossi@example.test');
    expect(row.textContent).toContain('כספים');
    expect(row.textContent).toContain('רכז שכר');
    expect(row.textContent).toContain('מנהל/ת מערכת');
    expect(row.textContent).toContain('גורם מטפל');
    expect(row.textContent).toContain('לא פעיל');
    expect(row.querySelector('a')?.getAttribute('href')).toBe('/admin/users/42');
  });

  describe('default order: employee code, descending', () => {
    function header(element: HTMLElement, text: string): HTMLElement {
      return Array.from(element.querySelectorAll('th')).find((th) => th.textContent!.includes(text))!;
    }

    it('is requested from the server and shown on the employee code header on load', async () => {
      usersApi.list.and.returnValue(of(result([fakeUserDetails()])));
      const element = await open('/admin/users');

      expect(usersApi.list).toHaveBeenCalledWith(jasmine.objectContaining({ sortBy: 'employeeCode', sortDir: 'desc' }));
      const code = header(element, 'קוד עובד');
      expect(code.getAttribute('aria-sort')).toBe('descending');
      expect(code.querySelector('.sort-button')!.classList).toContain('sorted');
      expect(code.querySelector('.sort-icon')!.textContent!.trim()).toBe('arrow_downward');
    });

    it('is not written to the URL; only a sort the user chose is', async () => {
      usersApi.list.and.returnValue(of(result([fakeUserDetails()])));
      const harness = await RouterTestingHarness.create();
      await harness.navigateByUrl('/admin/users', UsersList);
      harness.detectChanges();
      const router = TestBed.inject(Router);
      expect(router.url).toBe('/admin/users');

      // Same column, other direction: only sortDir.
      (header(harness.routeNativeElement!, 'קוד עובד').querySelector('.sort-button') as HTMLButtonElement).click();
      await harness.fixture.whenStable();
      expect(router.url).toBe('/admin/users?sortDir=asc');

      // Another column: sortBy (ascending is that column's default).
      harness.detectChanges();
      (header(harness.routeNativeElement!, 'מייל').querySelector('.sort-button') as HTMLButtonElement).click();
      await harness.fixture.whenStable();
      expect(router.url).toBe('/admin/users?sortBy=email');
    });
  });

  it('sortable headers expose aria-sort', async () => {
    usersApi.list.and.returnValue(of(result([fakeUserDetails()])));
    const element = await open('/admin/users?sortBy=email&sortDir=desc');
    const headers = Array.from(element.querySelectorAll('th'));
    const email = headers.find((th) => th.textContent!.includes('מייל'))!;
    const name = headers.find((th) => th.textContent!.trim().startsWith('שם'))!;
    expect(email.getAttribute('aria-sort')).toBe('descending');
    expect(name.getAttribute('aria-sort')).toBe('none');
  });
});
