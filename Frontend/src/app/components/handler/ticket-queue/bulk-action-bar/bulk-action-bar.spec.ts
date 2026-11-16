import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BulkActionBar } from './bulk-action-bar';

describe('BulkActionBar', () => {
  let fixture: ComponentFixture<BulkActionBar>;

  function render(count: number, running: 'start' | 'assign' | null = null): HTMLElement {
    TestBed.configureTestingModule({ imports: [BulkActionBar] });
    fixture = TestBed.createComponent(BulkActionBar);
    fixture.componentRef.setInput('count', count);
    fixture.componentRef.setInput('running', running);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const button = (element: HTMLElement, action: string) =>
    element.querySelector<HTMLButtonElement>(`button[data-action="${action}"]`)!;

  it('emits the actions', () => {
    const element = render(4);
    const start = jasmine.createSpy('start');
    const assign = jasmine.createSpy('assign');
    const clear = jasmine.createSpy('clear');
    fixture.componentInstance.start.subscribe(start);
    fixture.componentInstance.assign.subscribe(assign);
    fixture.componentInstance.clear.subscribe(clear);

    button(element, 'start').click();
    button(element, 'assign').click();
    button(element, 'clear').click();

    expect(element.textContent).toContain('נבחרו 4 פניות');
    expect([start.calls.count(), assign.calls.count(), clear.calls.count()]).toEqual([1, 1, 1]);
  });

  it('over 100 the actions are disabled and the limit is explained', () => {
    const element = render(101);

    expect(button(element, 'start').disabled).toBeTrue();
    expect(button(element, 'assign').disabled).toBeTrue();
    expect(button(element, 'clear').disabled).toBeFalse();
    expect(element.querySelector('[role="alert"]')!.textContent).toContain('ניתן לעדכן עד 100 פניות בפעולה אחת');
  });

  it('while an action runs every button is locked', () => {
    const element = render(3, 'start');

    expect(['start', 'assign', 'clear'].every((action) => button(element, action).disabled)).toBeTrue();
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });
});
