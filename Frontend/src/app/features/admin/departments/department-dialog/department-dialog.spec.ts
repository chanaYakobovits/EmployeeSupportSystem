import { ANIMATION_MODULE_TYPE } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { Department } from '../../../../core/models/department.models';
import { fakeDepartment, problemResponse } from '../../testing/admin-fixtures';
import { DepartmentDialog } from './department-dialog';

describe('DepartmentDialog', () => {
  let fixture: ComponentFixture<DepartmentDialog>;
  let element: HTMLElement;
  let departmentsApi: jasmine.SpyObj<DepartmentsApiService>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<DepartmentDialog, Department>>;

  function render(department: Department | null): void {
    departmentsApi = jasmine.createSpyObj<DepartmentsApiService>('DepartmentsApiService', ['create', 'update']);
    dialogRef = jasmine.createSpyObj<MatDialogRef<DepartmentDialog, Department>>('MatDialogRef', ['close']);
    const usersApi = jasmine.createSpyObj<UsersApiService>('UsersApiService', ['lookupActive']);
    usersApi.lookupActive.and.returnValue(of([]));
    TestBed.configureTestingModule({
      imports: [DepartmentDialog],
      providers: [
        provideRouter([]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: MAT_DIALOG_DATA, useValue: { department } },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: DepartmentsApiService, useValue: departmentsApi },
        { provide: UsersApiService, useValue: usersApi },
      ],
    });
    fixture = TestBed.createComponent(DepartmentDialog);
    fixture.detectChanges();
    element = fixture.nativeElement;
  }

  function fieldError(control: string): string {
    const field = element.querySelector(`input[formcontrolname="${control}"]`)!.closest('mat-form-field')!;
    return field.querySelector('mat-error')?.textContent?.trim() ?? '';
  }

  function submit(): void {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  it('creates a department without a manager and closes with it', () => {
    render(null);
    const created = fakeDepartment({ id: 9, name: 'רכש' });
    departmentsApi.create.and.returnValue(of(created));
    const input = element.querySelector('input[formcontrolname="name"]') as HTMLInputElement;
    input.value = 'רכש';
    input.dispatchEvent(new Event('input'));
    submit();

    expect(departmentsApi.create).toHaveBeenCalledOnceWith({ name: 'רכש', managerUserId: null });
    expect(dialogRef.close).toHaveBeenCalledOnceWith(created);
  });

  it('409: a taken name is shown next to the name field', () => {
    render(fakeDepartment());
    departmentsApi.update.and.returnValue(
      throwError(() =>
        problemResponse(409, 'duplicate-department-name', 'כבר קיימת מחלקה בשם זה', {
          errors: { name: ['כבר קיימת מחלקה בשם זה'] },
        }),
      ),
    );
    submit();
    expect(fieldError('name')).toContain('כבר קיימת מחלקה בשם זה');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });

  it('a manager who already manages another department: the message is shown next to the manager field', () => {
    render(
      fakeDepartment({ manager: { id: 5, employeeCode: 'EMP-00005', firstName: 'רונית', lastName: 'לוי' } }),
    );
    departmentsApi.update.and.returnValue(
      throwError(() =>
        problemResponse(422, 'manages-other-department', 'העובד כבר מנהל את מחלקת משאבי אנוש', {
          detail: 'עובד יכול לנהל מחלקה אחת בלבד.',
        }),
      ),
    );
    submit();

    expect(departmentsApi.update).toHaveBeenCalledOnceWith(
      2,
      jasmine.objectContaining({ managerUserId: 5, isActive: true }),
    );
    expect(fieldError('manager')).toContain('העובד כבר מנהל את מחלקת משאבי אנוש');
    expect(element.querySelector('app-form-alert')).toBeNull();
  });

  it('text typed in the manager field without choosing an employee is an error', () => {
    render(null);
    const name = element.querySelector('input[formcontrolname="name"]') as HTMLInputElement;
    name.value = 'רכש';
    name.dispatchEvent(new Event('input'));
    const manager = element.querySelector('input[formcontrolname="manager"]') as HTMLInputElement;
    manager.value = 'רונ';
    manager.dispatchEvent(new Event('input'));
    submit();

    expect(departmentsApi.create).not.toHaveBeenCalled();
    expect(fieldError('manager')).toContain('יש לבחור עובד מהרשימה');
  });
});
