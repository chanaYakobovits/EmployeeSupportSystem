import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';

import { fakeFile } from '../../employee/testing/ticket-fixtures';
import { FileUpload } from './file-upload';

@Component({
  imports: [ReactiveFormsModule, FileUpload],
  template: `<app-file-upload [formControl]="files" [serverError]="serverError()" />`,
})
class Host {
  readonly files = new FormControl<File[]>([], { nonNullable: true });
  readonly serverError = signal<string | null>(null);
}

describe('FileUpload', () => {
  const MB = 1024 * 1024;
  let fixture: ComponentFixture<Host>;
  let element: HTMLElement;

  beforeEach(() => {
    fixture = TestBed.createComponent(Host);
    fixture.autoDetectChanges();
    element = fixture.nativeElement;
    document.body.appendChild(element);
  });

  afterEach(() => element.remove());

  const control = () => fixture.componentInstance.files;
  const input = () => element.querySelector<HTMLInputElement>('input[type="file"]')!;
  const names = () => Array.from(element.querySelectorAll('.file-name')).map((e) => e.textContent!.trim());
  const messages = () => element.querySelector('.messages')!;

  async function pick(...files: File[]): Promise<void> {
    const transfer = new DataTransfer();
    files.forEach((file) => transfer.items.add(file));
    input().files = transfer.files;
    input().dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  it('the input accepts only the allowed types, several at once', () => {
    expect(input().accept).toBe('.pdf,.jpg,.jpeg,.png,.docx');
    expect(input().multiple).toBeTrue();
  });

  it('the visible button opens the file dialog (keyboard, no dragging needed)', () => {
    const click = spyOn(input(), 'click');
    element.querySelector<HTMLButtonElement>('.choose-button')!.click();
    expect(click).toHaveBeenCalled();
  });

  it('chosen files become the control value and are listed with their size', async () => {
    await pick(fakeFile('תלוש.pdf', 350 * 1024), fakeFile('צילום.png', 1.2 * MB));
    expect(control().value.map((f) => f.name)).toEqual(['תלוש.pdf', 'צילום.png']);
    expect(names()).toEqual(['תלוש.pdf', 'צילום.png']);
    const sizes = Array.from(element.querySelectorAll('.file-size')).map((e) => e.textContent!.trim());
    expect(sizes).toEqual(['350 KB', '1.2 MB']);
    expect(input().value).toBe('');
  });

  it('a file over 5MB is rejected with a Hebrew message, in a live region', async () => {
    await pick(fakeFile('גדול.pdf', 6 * MB));
    expect(control().value).toEqual([]);
    expect(messages().getAttribute('aria-live')).toBe('assertive');
    expect(messages().textContent).toContain('גדול.pdf');
    expect(messages().textContent).toContain('גודל כל קובץ: עד 5MB');
  });

  it('a type that is not allowed is rejected', async () => {
    await pick(fakeFile('setup.exe'), fakeFile('ok.docx'));
    expect(control().value.map((f) => f.name)).toEqual(['ok.docx']);
    expect(messages().textContent).toContain('setup.exe');
    expect(messages().textContent).toContain('ניתן לצרף רק קבצי PDF, JPG, PNG או DOCX');
  });

  it('4 files: 3 are added, the 4th is rejected', async () => {
    await pick(fakeFile('1.pdf'), fakeFile('2.pdf'), fakeFile('3.pdf'), fakeFile('4.pdf'));
    expect(control().value.length).toBe(3);
    expect(messages().textContent).toContain('4.pdf');
    expect(messages().textContent).toContain('ניתן לצרף עד 3 קבצים');
    expect(element.querySelector('.count')!.textContent).toContain('המקסימום');
  });

  it('removing a file takes it out of the value, and the focus moves to the next remove button', async () => {
    await pick(fakeFile('1.pdf'), fakeFile('2.pdf'), fakeFile('3.pdf'));
    element.querySelectorAll<HTMLButtonElement>('.remove-button')[0].click();
    await fixture.whenStable();

    expect(control().value.map((f) => f.name)).toEqual(['2.pdf', '3.pdf']);
    expect(names()).toEqual(['2.pdf', '3.pdf']);
    expect(document.activeElement).toBe(element.querySelectorAll('.remove-button')[0]);
    expect(element.querySelectorAll('.remove-button')[0].getAttribute('aria-label')).toBe('הסרת הקובץ 2.pdf');
  });

  it('removing the last file moves the focus back to "choose"', async () => {
    await pick(fakeFile('1.pdf'));
    element.querySelector<HTMLButtonElement>('.remove-button')!.click();
    await fixture.whenStable();
    expect(control().value).toEqual([]);
    expect(document.activeElement).toBe(element.querySelector('.choose-button'));
  });

  it('dropped files are checked the same way', async () => {
    const transfer = new DataTransfer();
    transfer.items.add(fakeFile('a.png'));
    transfer.items.add(fakeFile('b.exe'));
    element.querySelector('.drop-zone')!.dispatchEvent(new DragEvent('drop', { dataTransfer: transfer, cancelable: true }));
    await fixture.whenStable();
    expect(control().value.map((f) => f.name)).toEqual(['a.png']);
    expect(messages().textContent).toContain('b.exe');
  });

  it('resetting the control clears the list and the messages', async () => {
    await pick(fakeFile('a.png'), fakeFile('b.exe'));
    control().reset([]);
    await fixture.whenStable();
    expect(names()).toEqual([]);
    expect(messages().textContent!.trim()).toBe('');
  });

  it('a disabled control disables the buttons', async () => {
    await pick(fakeFile('a.png'));
    control().disable();
    await fixture.whenStable();
    expect(element.querySelector<HTMLButtonElement>('.choose-button')!.disabled).toBeTrue();
    expect(element.querySelector<HTMLButtonElement>('.remove-button')!.disabled).toBeTrue();
  });

  it('shows an error from the server in the same live region', async () => {
    fixture.componentInstance.serverError.set('צילום.png: תוכן הקובץ אינו תואם לסוג הקובץ');
    await fixture.whenStable();
    expect(messages().textContent).toContain('תוכן הקובץ אינו תואם לסוג הקובץ');
  });
});
