import { ANIMATION_MODULE_TYPE, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { fakeUser } from '../../../../core/auth/testing/auth-fixtures';
import { UserDetails } from '../../../../core/models/user.models';
import { NotificationService } from '../../../../core/notifications/notification.service';
import { ConfirmService } from '../../../../shared/components/confirm-dialog/confirm.service';
import { fakeDepartment, fakeUserDetails, problemResponse } from '../../testing/admin-fixtures';
import { UserForm } from './user-form';

describe('UserForm', () => {
  let fixture: ComponentFixture<UserForm>;
  let element: HTMLElement;
  let usersApi: jasmine.SpyObj<UsersApiService>;
  let confirm: jasmine.SpyObj<ConfirmService>;
  const SIGNED_IN_ADMIN_ID = 7;

  beforeEach(() => {
    usersApi = jasmine.createSpyObj<UsersApiService>('UsersApiService', [
      'get',
      'create',
      'update',
      'resetPassword',
      'setActive',
      'resendInvitation',
    ]);
    confirm = jasmine.createSpyObj<ConfirmService>('ConfirmService', ['confirm']);
    const departmentsApi = jasmine.createSpyObj<DepartmentsApiService>('DepartmentsApiService', ['list']);
    departmentsApi.list.and.returnValue(
      of([fakeDepartment({ id: 2, name: 'כספים' }), fakeDepartment({ id: 3, name: 'משאבי אנוש' })]),
    );

    TestBed.configureTestingModule({
      imports: [UserForm],
      providers: [
        provideRouter([]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: UsersApiService, useValue: usersApi },
        { provide: DepartmentsApiService, useValue: departmentsApi },
        { provide: ConfirmService, useValue: confirm },
        { provide: NotificationService, useValue: jasmine.createSpyObj('NotificationService', ['success', 'error']) },
        {
          provide: AuthService,
          useValue: { currentUser: signal(fakeUser({ id: SIGNED_IN_ADMIN_ID, roles: ['Employee', 'Admin'] })) },
        },
      ],
    });
  });

  function render(user: UserDetails | null): void {
    if (user) {
      usersApi.get.and.returnValue(of(user));
    }
    fixture = TestBed.createComponent(UserForm);
    if (user) {
      fixture.componentRef.setInput('id', String(user.id));
    }
    fixture.detectChanges();
    element = fixture.nativeElement;
  }

  function checkbox(selector: string): HTMLInputElement {
    return element.querySelector(`${selector} input[type="checkbox"]`) as HTMLInputElement;
  }

  function roleCheckbox(name: string): HTMLInputElement {
    return checkbox(`mat-checkbox[formcontrolname="${name}"]`);
  }

  function hasButton(text: string): boolean {
    return Array.from(element.querySelectorAll('button')).some((b) => b.textContent!.includes(text));
  }

  function submit(): void {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  function type(control: string, value: string): void {
    const input = element.querySelector(`input[formcontrolname="${control}"]`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  describe('create', () => {
    beforeEach(() => render(null));

    it('has the fields of spec 4.2 and no employee code or password', () => {
      for (const name of ['firstName', 'lastName', 'email', 'jobTitle']) {
        expect(element.querySelector(`input[formcontrolname="${name}"]`)).withContext(name).not.toBeNull();
      }
      expect(element.querySelector('mat-select[formcontrolname="departmentId"]')).not.toBeNull();
      expect(element.textContent).not.toContain('קוד עובד');
      expect(element.querySelector('input[type="password"]')).toBeNull();
    });

    it('Employee is checked and locked', () => {
      const employee = checkbox('mat-checkbox.role-employee');
      expect(employee.checked).toBeTrue();
      expect(employee.disabled).toBeTrue();
    });

    it('DepartmentManager cannot be selected and explains why', () => {
      const manager = checkbox('mat-checkbox.role-department-manager');
      expect(manager.disabled).toBeTrue();
      expect(manager.checked).toBeFalse();
      manager.click();
      fixture.detectChanges();
      expect(manager.checked).toBeFalse();
      expect(element.textContent).toContain('נקבע לפי מינוי במסך המחלקות');
    });

    it('shows required errors next to the fields and does not call the server', () => {
      submit();
      expect(usersApi.create).not.toHaveBeenCalled();
      expect(element.textContent).toContain('יש להזין שם פרטי');
      expect(element.textContent).toContain('יש לבחור מחלקה');
    });
  });

  describe('edit', () => {
    it('shows the employee code disabled, with the explanation, and never sends it', () => {
      const user = fakeUserDetails();
      usersApi.update.and.returnValue(of(user));
      render(user);

      const code = element.querySelector('input[formcontrolname="employeeCode"]') as HTMLInputElement;
      expect(code.value).toBe('EMP-00042');
      expect(code.disabled).toBeTrue();
      expect(code.readOnly).toBeFalse();
      expect(code.closest('mat-form-field')!.querySelector('mat-hint')?.textContent).toContain(
        'נוצר אוטומטית ואינו ניתן לשינוי',
      );

      submit();
      const request = usersApi.update.calls.mostRecent().args[1];
      expect(Object.keys(request)).not.toContain('employeeCode');
    });

    it('DepartmentManager: shown from the server, read-only, and sent back unchanged', () => {
      const manager = fakeUserDetails({
        roles: ['Employee', 'DepartmentManager', 'Handler'],
        managedDepartment: { id: 2, name: 'כספים' },
      });
      usersApi.update.and.returnValue(of(manager));
      render(manager);

      const checkboxElement = checkbox('mat-checkbox.role-department-manager');
      expect(checkboxElement.checked).toBeTrue();
      expect(checkboxElement.disabled).toBeTrue();
      checkboxElement.click();
      fixture.detectChanges();
      expect(checkboxElement.checked).toBeTrue();
      expect(element.textContent).toContain('מנהל/ת את מחלקת כספים');

      roleCheckbox('Handler').click();
      fixture.detectChanges();
      submit();

      expect(usersApi.update).toHaveBeenCalledOnceWith(
        42,
        jasmine.objectContaining({ roles: ['Employee', 'DepartmentManager'] }),
      );
    });

    it('never adds DepartmentManager to a user who is not a manager', () => {
      const user = fakeUserDetails({ roles: ['Employee'] });
      usersApi.update.and.returnValue(of(user));
      render(user);
      roleCheckbox('Admin').click();
      fixture.detectChanges();
      submit();
      expect(usersApi.update).toHaveBeenCalledOnceWith(42, jasmine.objectContaining({ roles: ['Employee', 'Admin'] }));
    });

    it('409 on the e-mail is shown next to the e-mail field', () => {
      render(fakeUserDetails());
      usersApi.update.and.returnValue(
        throwError(() =>
          problemResponse(409, 'duplicate-email', 'כתובת המייל כבר רשומה במערכת', {
            errors: { email: ['כתובת המייל כבר רשומה במערכת'] },
          }),
        ),
      );
      type('email', 'taken@example.test');
      submit();

      const field = element.querySelector('input[formcontrolname="email"]')!.closest('mat-form-field')!;
      expect(field.querySelector('mat-error')?.textContent).toContain('כתובת המייל כבר רשומה במערכת');
      expect(element.querySelector('app-form-alert')).toBeNull();
    });

    it('offers reset password and deactivate for another user', () => {
      render(fakeUserDetails());
      expect(hasButton('איפוס סיסמה')).toBeTrue();
      expect(hasButton('השבתת העובד')).toBeTrue();
    });

    it('the signed-in admin: no deactivate, no reset, and Admin cannot be removed', () => {
      const self = fakeUserDetails({ id: SIGNED_IN_ADMIN_ID, roles: ['Employee', 'Admin'] });
      usersApi.update.and.returnValue(of(self));
      render(self);

      expect(hasButton('השבתת העובד')).toBeFalse();
      expect(hasButton('איפוס סיסמה')).toBeFalse();
      const admin = roleCheckbox('Admin');
      expect(admin.checked).toBeTrue();
      expect(admin.disabled).toBeTrue();

      submit();
      expect(usersApi.update).toHaveBeenCalledOnceWith(
        SIGNED_IN_ADMIN_ID,
        jasmine.objectContaining({ roles: ['Employee', 'Admin'] }),
      );
    });

    it('deactivating a department manager (422) shows the server message with a link to departments', () => {
      render(fakeUserDetails({ roles: ['Employee', 'DepartmentManager'], managedDepartment: { id: 2, name: 'כספים' } }));
      confirm.confirm.and.returnValue(of(true));
      usersApi.setActive.and.returnValue(
        throwError(() =>
          problemResponse(422, 'department-manager-deactivation', 'העובד מנהל את מחלקת כספים', {
            detail: 'יש להחליף קודם את מנהל המחלקה במסך המחלקות, ואז להשבית.',
          }),
        ),
      );

      Array.from(element.querySelectorAll('button'))
        .find((b) => b.textContent!.includes('השבתת העובד'))!
        .click();
      fixture.detectChanges();

      expect(usersApi.setActive).toHaveBeenCalledOnceWith(42, false);
      const alert = element.querySelector('app-form-alert')!;
      expect(alert.textContent).toContain('העובד מנהל את מחלקת כספים');
      expect(alert.querySelector('a')?.getAttribute('href')).toBe('/admin/departments');
    });

    it('a cancelled confirmation does nothing', () => {
      render(fakeUserDetails());
      confirm.confirm.and.returnValue(of(false));
      Array.from(element.querySelectorAll('button'))
        .find((b) => b.textContent!.includes('איפוס סיסמה'))!
        .click();
      expect(usersApi.resetPassword).not.toHaveBeenCalled();
    });

    describe('invitation', () => {
      const AT = '2026-11-12T08:30:00Z';

      function button(text: string): HTMLButtonElement {
        return Array.from(element.querySelectorAll('button')).find((b) => b.textContent!.includes(text))!;
      }

      function invitationSection(): HTMLElement {
        return element.querySelector('section.invitation')!;
      }

      it('shows the state and the last time', () => {
        render(fakeUserDetails({ mustChangePassword: true, invitation: { status: 'Sent', at: AT } }));

        expect(invitationSection().querySelector('app-invitation-badge')!.textContent).toContain('נשלחה');
        expect(invitationSection().textContent).toContain('12/11/2026 10:30');
      });

      it('resend: explains that the previous link stops working, then shows the new state', () => {
        const user = fakeUserDetails({ mustChangePassword: true, invitation: { status: 'Sent', at: AT } });
        render(user);
        confirm.confirm.and.returnValue(of(true));
        usersApi.resendInvitation.and.returnValue(of({ ...user, invitation: { status: 'Pending', at: AT } }));

        button('שליחת הזמנה מחדש').click();
        fixture.detectChanges();

        const options = confirm.confirm.calls.mostRecent().args[0];
        expect(options.consequences!.join(' ')).toContain('הקישור מההזמנה הקודמת יפסיק לעבוד');
        expect(usersApi.resendInvitation).toHaveBeenCalledOnceWith(42);
        expect(invitationSection().textContent).toContain('ממתינה');
      });

      it('a cancelled resend confirmation sends nothing', () => {
        render(fakeUserDetails({ mustChangePassword: true, invitation: { status: 'Expired', at: AT } }));
        confirm.confirm.and.returnValue(of(false));

        button('שליחת הזמנה מחדש').click();

        expect(usersApi.resendInvitation).not.toHaveBeenCalled();
      });

      it('no resend once the invitation was used (or the user chose a password)', () => {
        render(fakeUserDetails({ mustChangePassword: false, invitation: { status: 'Used', at: AT } }));

        expect(hasButton('שליחת הזמנה')).toBeFalse();
        expect(invitationSection().textContent).toContain('נוצלה');
      });

      it('no resend for an inactive user', () => {
        render(fakeUserDetails({ isActive: false, mustChangePassword: true, invitation: { status: 'Sent', at: AT } }));

        expect(hasButton('שליחת הזמנה')).toBeFalse();
      });

      it('failed: a prominent warning with resend and a temporary password instead', () => {
        render(fakeUserDetails({ mustChangePassword: true, invitation: { status: 'Failed', at: AT } }));

        const warning = element.querySelector('.invitation-failed')!;
        expect(warning.textContent).toContain('ההזמנה לא נשלחה');
        expect(warning.textContent).toContain('שליחה מחדש');
        expect(warning.textContent).toContain('סיסמה זמנית במקום');
      });

      it('failed → temporary password: the confirmation says the invitation is cancelled', () => {
        render(fakeUserDetails({ mustChangePassword: true, invitation: { status: 'Failed', at: AT } }));
        confirm.confirm.and.returnValue(of(false));

        button('סיסמה זמנית במקום').click();

        const options = confirm.confirm.calls.mostRecent().args[0];
        expect(options.heading).toBe('איפוס סיסמה');
        expect(options.consequences!.join(' ')).toContain('ההזמנה במייל תבוטל');
      });

      it('a reset reloads the user, so the state becomes "בוטלה"', async () => {
        const user = fakeUserDetails({ mustChangePassword: true, invitation: { status: 'Sent', at: AT } });
        render(user);
        confirm.confirm.and.returnValue(of(true));
        usersApi.resetPassword.and.returnValue(of({ temporaryPassword: 'Temp12345abc' }));
        usersApi.get.and.returnValue(of({ ...user, invitation: { status: 'Revoked', at: AT } }));

        button('איפוס סיסמה').click();
        fixture.detectChanges();
        await fixture.whenStable();

        expect(usersApi.get).toHaveBeenCalledTimes(2);
        expect(invitationSection().textContent).toContain('בוטלה');
      });

      it('422 (already chose a password) shows the server message', () => {
        render(fakeUserDetails({ mustChangePassword: true, invitation: { status: 'Sent', at: AT } }));
        confirm.confirm.and.returnValue(of(true));
        usersApi.resendInvitation.and.returnValue(
          throwError(() =>
            problemResponse(422, 'password-already-set', 'העובד כבר קבע סיסמה', {
              detail: 'אפשר לאפס לו סיסמה במקום לשלוח הזמנה.',
            }),
          ),
        );

        button('שליחת הזמנה מחדש').click();
        fixture.detectChanges();

        const alert = element.querySelector('app-form-alert')!;
        expect(alert.textContent).toContain('העובד כבר קבע סיסמה');
        expect(alert.textContent).toContain('אפשר לאפס לו סיסמה');
      });

      it('not shown for the signed-in admin', () => {
        render(fakeUserDetails({ id: SIGNED_IN_ADMIN_ID, roles: ['Employee', 'Admin'] }));

        expect(element.querySelector('section.invitation')).toBeNull();
      });
    });

    it('an id that is not a number is "not found"', () => {
      fixture = TestBed.createComponent(UserForm);
      fixture.componentRef.setInput('id', 'abc');
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).toContain('העובד לא נמצא');
      expect(usersApi.get).not.toHaveBeenCalled();
    });
  });
});
