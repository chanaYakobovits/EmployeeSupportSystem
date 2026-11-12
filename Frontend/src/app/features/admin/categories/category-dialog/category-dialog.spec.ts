import { ANIMATION_MODULE_TYPE } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { CategoriesApiService } from '../../../../core/api/categories-api.service';
import { DepartmentsApiService } from '../../../../core/api/departments-api.service';
import { Category, CategorySaveResult } from '../../../../core/models/category.models';
import { fakeCategory, fakeDepartment, problemResponse } from '../../testing/admin-fixtures';
import { CategoryDialog } from './category-dialog';

describe('CategoryDialog', () => {
  let fixture: ComponentFixture<CategoryDialog>;
  let element: HTMLElement;
  let categoriesApi: jasmine.SpyObj<CategoriesApiService>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<CategoryDialog, CategorySaveResult>>;

  function render(category: Category | null): void {
    categoriesApi = jasmine.createSpyObj<CategoriesApiService>('CategoriesApiService', ['create', 'update']);
    dialogRef = jasmine.createSpyObj<MatDialogRef<CategoryDialog, CategorySaveResult>>('MatDialogRef', ['close']);
    const departmentsApi = jasmine.createSpyObj<DepartmentsApiService>('DepartmentsApiService', ['list']);
    departmentsApi.list.and.returnValue(
      of([fakeDepartment({ id: 3, name: 'הנהלת חשבונות' }), fakeDepartment({ id: 6, name: 'משאבי אנוש מרכזי' })]),
    );
    TestBed.configureTestingModule({
      imports: [CategoryDialog],
      providers: [
        provideRouter([]),
        { provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' },
        { provide: MAT_DIALOG_DATA, useValue: { category } },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: CategoriesApiService, useValue: categoriesApi },
        { provide: DepartmentsApiService, useValue: departmentsApi },
      ],
    });
    fixture = TestBed.createComponent(CategoryDialog);
    fixture.detectChanges();
    element = fixture.nativeElement;
  }

  function input(name: string): HTMLInputElement | HTMLTextAreaElement {
    return element.querySelector(`[formcontrolname="${name}"]`) as HTMLInputElement;
  }

  function type(name: string, value: string): void {
    const field = input(name);
    field.value = value;
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  /** Sets a mat-select through its form control (the overlay is not part of this test). */
  function choose(name: 'routingType' | 'handlingDepartment', value: unknown): void {
    const form = (fixture.componentInstance as unknown as { form: { controls: Record<string, { setValue(v: unknown): void }> } })
      .form;
    form.controls[name].setValue(value);
    fixture.detectChanges();
  }

  function handlingDepartmentField(): Element | null {
    return element.querySelector('[formcontrolname="handlingDepartment"]');
  }

  function fieldError(name: string): string {
    return input(name).closest('mat-form-field')!.querySelector('mat-error')?.textContent?.trim() ?? '';
  }

  function submit(): void {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  }

  describe('handling department field', () => {
    it('is hidden until a routing type is chosen', () => {
      render(null);
      expect(handlingDepartmentField()).toBeNull();
    });

    it('is shown and required for FixedDepartment', () => {
      render(null);
      type('code', 'HOME_OFFICE');
      type('name', 'עבודה מהבית');
      type('description', 'ציוד לעבודה מהבית');
      choose('routingType', 'FixedDepartment');

      expect(handlingDepartmentField()).not.toBeNull();
      submit();
      expect(categoriesApi.create).not.toHaveBeenCalled();
      expect(handlingDepartmentField()!.closest('mat-form-field')!.textContent).toContain('יש לבחור מחלקה מטפלת');
    });

    it('disappears for RequesterDepartmentHr and the request has no department', () => {
      render(fakeCategory());
      expect(handlingDepartmentField()).not.toBeNull();

      choose('routingType', 'RequesterDepartmentHr');
      expect(handlingDepartmentField()).toBeNull();

      categoriesApi.update.and.returnValue(of({ category: fakeCategory(), warnings: [] }));
      submit();
      expect(categoriesApi.update).toHaveBeenCalledOnceWith(
        1,
        jasmine.objectContaining({ routingType: 'RequesterDepartmentHr', handlingDepartmentId: null }),
      );
    });

    it('comes back empty when switching back to FixedDepartment', () => {
      render(fakeCategory());
      choose('routingType', 'RequesterDepartmentHr');
      choose('routingType', 'FixedDepartment');

      expect(handlingDepartmentField()).not.toBeNull();
      submit();
      expect(categoriesApi.update).not.toHaveBeenCalled();
    });
  });

  describe('code', () => {
    it('is editable when creating, converted to upper case, and sent', () => {
      render(null);
      expect(input('code').disabled).toBeFalse();

      type('code', 'home_office');
      expect(input('code').value).toBe('HOME_OFFICE');

      type('name', 'עבודה מהבית');
      type('description', 'ציוד לעבודה מהבית');
      choose('routingType', 'FixedDepartment');
      choose('handlingDepartment', 6);
      const saved = { category: fakeCategory({ id: 9, code: 'HOME_OFFICE' }), warnings: [] };
      categoriesApi.create.and.returnValue(of(saved));
      submit();

      expect(categoriesApi.create).toHaveBeenCalledOnceWith({
        code: 'HOME_OFFICE',
        name: 'עבודה מהבית',
        description: 'ציוד לעבודה מהבית',
        routingType: 'FixedDepartment',
        handlingDepartmentId: 6,
      });
      expect(dialogRef.close).toHaveBeenCalledOnceWith(saved);
    });

    it('is disabled when editing and not sent', () => {
      render(fakeCategory());
      expect(input('code').disabled).toBeTrue();
      expect(input('code').value).toBe('PAYROLL');

      categoriesApi.update.and.returnValue(of({ category: fakeCategory(), warnings: [] }));
      submit();
      const request = categoriesApi.update.calls.mostRecent().args[1];
      expect('code' in request).toBeFalse();
    });

    it('rejects a code that is not upper-case English and underscores', () => {
      render(null);
      type('code', 'HOME-1');
      submit();
      expect(fieldError('code')).toContain('אותיות אנגליות גדולות');
    });

    it('409: a taken code is shown next to the code field', () => {
      render(null);
      type('code', 'PAYROLL');
      type('name', 'שכר');
      type('description', 'תיאור');
      choose('routingType', 'RequesterDepartmentHr');
      categoriesApi.create.and.returnValue(
        throwError(() =>
          problemResponse(409, 'duplicate-category-code', 'כבר קיימת קטגוריה עם קוד זה', {
            errors: { code: ['כבר קיימת קטגוריה עם קוד זה'] },
          }),
        ),
      );
      submit();

      expect(fieldError('code')).toContain('כבר קיימת קטגוריה עם קוד זה');
      expect(dialogRef.close).not.toHaveBeenCalled();
    });
  });

  it('shows the AI privacy note and a character counter under the description', () => {
    render(fakeCategory({ description: 'abc' }));
    expect(element.textContent).toContain('התיאור נשלח ל-AI לצורך סיווג. אין לכלול בו שמות או פרטים אישיים.');
    expect(element.textContent).toContain('3/500');
  });
});
