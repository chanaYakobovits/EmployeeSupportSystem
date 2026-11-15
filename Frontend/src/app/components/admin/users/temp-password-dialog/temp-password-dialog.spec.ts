import { Clipboard } from '@angular/cdk/clipboard';
import { ANIMATION_MODULE_TYPE, ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';

import { TemporaryPasswordData, TemporaryPasswordDialog, openTemporaryPasswordDialog } from './temp-password-dialog';

describe('TemporaryPasswordDialog', () => {
  let dialog: MatDialog;
  let ref: MatDialogRef<TemporaryPasswordDialog, void>;
  let data: TemporaryPasswordData;
  let closed: boolean;
  let copy: jasmine.Spy;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [{ provide: ANIMATION_MODULE_TYPE, useValue: 'NoopAnimations' }] });
    copy = spyOn(TestBed.inject(Clipboard), 'copy').and.returnValue(true);
    dialog = TestBed.inject(MatDialog);
    data = { employeeCode: 'EMP-00042', fullName: 'יוסי כהן', temporaryPassword: 'Xy7#kP2q', reason: 'created' };
    ref = openTemporaryPasswordDialog(dialog, data);
    closed = false;
    ref.afterClosed().subscribe(() => (closed = true));
    render();
  });

  afterEach(() => dialog.closeAll());

  function render(): void {
    TestBed.inject(ApplicationRef).tick();
  }

  function container(): HTMLElement {
    return document.querySelector('app-temp-password-dialog') as HTMLElement;
  }

  function button(text: string): HTMLButtonElement {
    return Array.from(container().querySelectorAll('button')).find((b) => b.textContent!.includes(text))!;
  }

  it('shows the employee code, the password and the warning', () => {
    const text = container().textContent!;
    expect(text).toContain('EMP-00042');
    expect(text).toContain('Xy7#kP2q');
    expect(text).toContain('הסיסמה לא תוצג שוב');
  });

  it('does not close with Esc', () => {
    document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true }));
    render();
    expect(dialog.openDialogs.length).toBe(1);
    expect(closed).toBeFalse();
  });

  it('does not close with a click outside', () => {
    const backdrop = document.querySelector('.cdk-overlay-backdrop') as HTMLElement;
    expect(backdrop).not.toBeNull();
    backdrop.click();
    render();
    expect(dialog.openDialogs.length).toBe(1);
    expect(closed).toBeFalse();
  });

  it('disableClose is enforced by the dialog itself', () => {
    expect(ref.disableClose).toBeTrue();
  });

  it('copies the code and the password', () => {
    button('העתקת הפרטים').click();
    render();
    expect(copy).toHaveBeenCalledOnceWith('קוד עובד: EMP-00042\nסיסמה זמנית: Xy7#kP2q');
    expect(container().textContent).toContain('הפרטים הועתקו');
  });

  it('closes only with "העתקתי, סגור", and keeps no copy of the password', async () => {
    // The opener's data object no longer holds the password.
    expect(data.temporaryPassword).toBe('');

    const instance = ref.componentInstance as unknown as { password: () => string };
    button('העתקתי, סגור').click();
    render();
    await ref.afterClosed().toPromise();

    expect(closed).toBeTrue();
    expect(instance.password()).toBe('');
  });

  it('never writes the password to the console', () => {
    const spies = (['log', 'info', 'warn', 'error', 'debug'] as const).map((method) => spyOn(console, method));
    button('העתקת הפרטים').click();
    button('העתקתי, סגור').click();
    render();
    for (const spy of spies) {
      expect(spy).not.toHaveBeenCalled();
    }
  });
});
