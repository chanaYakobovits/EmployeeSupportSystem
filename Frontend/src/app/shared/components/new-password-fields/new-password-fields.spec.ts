import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ANIMATION_MODULE_TYPE } from '@angular/core';

import { newPasswordControls } from '../../../core/auth/password-policy';
import { NewPasswordFields } from './new-password-fields';

@Component({
  imports: [ReactiveFormsModule, NewPasswordFields],
  template: `
    <form [formGroup]="form" (ngSubmit)="submitted = true">
      <app-new-password-fields newPasswordLabel="סיסמה" />
      <button type="submit">שמירה</button>
    </form>
  `,
})
class HostForm {
  readonly form = inject(NonNullableFormBuilder).group(newPasswordControls());
  submitted = false;
}

describe('NewPasswordFields', () => {
  let fixture: ComponentFixture<HostForm>;
  let element: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HostForm],
      providers: [{ provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' }],
    });
    fixture = TestBed.createComponent(HostForm);
    fixture.detectChanges();
    element = fixture.nativeElement;
  });

  function input(name: string): HTMLInputElement {
    return element.querySelector(`input[formcontrolname="${name}"]`) as HTMLInputElement;
  }

  function type(name: string, value: string): void {
    input(name).value = value;
    input(name).dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function ruleStates(): (string | null)[] {
    return Array.from(element.querySelectorAll('.rules li')).map((li) => li.getAttribute('data-state'));
  }

  it('both fields are labelled, ask for a new password and are bound to the parent form', () => {
    expect(input('newPassword').getAttribute('autocomplete')).toBe('new-password');
    expect(input('confirmPassword').getAttribute('autocomplete')).toBe('new-password');
    expect(element.textContent).toContain('סיסמה'); // custom label
    expect(element.textContent).toContain('אימות סיסמה חדשה'); // default label

    type('newPassword', 'Abcdef12');
    expect(fixture.componentInstance.form.controls.newPassword.value).toBe('Abcdef12');
  });

  it('shows the policy up front and checks it live; the rules describe the field', () => {
    expect(ruleStates()).toEqual(['pending', 'pending', 'pending']);
    const rulesId = element.querySelector('.rules')!.id;
    expect(input('newPassword').getAttribute('aria-describedby')).toContain(rulesId);

    type('newPassword', 'abc');
    expect(ruleStates()).toEqual(['unmet', 'met', 'unmet']);

    type('newPassword', 'abcdefg1');
    expect(ruleStates()).toEqual(['met', 'met', 'met']);
  });

  it('shows the strength meter', () => {
    type('newPassword', 'Abcdefgh12!xyz');
    expect(element.querySelector('app-password-strength')!.textContent).toContain('חוזק הסיסמה');
  });

  it('a mismatch is an error on the confirmation, re-checked when the new password changes', () => {
    type('newPassword', 'abcdefg1');
    type('confirmPassword', 'abcdefg2');
    const confirm = fixture.componentInstance.form.controls.confirmPassword;
    expect(confirm.hasError('mismatch')).toBeTrue();

    type('newPassword', 'abcdefg2');
    expect(confirm.hasError('mismatch')).toBeFalse();
  });

  it('errors appear after the parent form is submitted', () => {
    (element.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.componentInstance.submitted).toBeTrue();
    expect(element.textContent).toContain('יש להזין סיסמה חדשה');
    expect(element.textContent).toContain('יש להזין שוב את הסיסמה החדשה');
  });

  it('the show / hide buttons switch the input type and say what they do', () => {
    const toggle = input('newPassword').closest('mat-form-field')!.querySelector('button')!;
    expect(input('newPassword').type).toBe('password');
    expect(toggle.getAttribute('aria-label')).toBe('הצגת הסיסמה החדשה');

    toggle.click();
    fixture.detectChanges();

    expect(input('newPassword').type).toBe('text');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');
  });
});
