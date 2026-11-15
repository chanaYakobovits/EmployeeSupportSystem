import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';

import { SEARCH_DEBOUNCE_MS, SearchField } from './search-field';

describe('SearchField', () => {
  let fixture: ComponentFixture<SearchField>;
  let input: HTMLInputElement;
  let emitted: string[];

  beforeEach(() => {
    fixture = TestBed.createComponent(SearchField);
    emitted = [];
    fixture.componentInstance.search.subscribe((value) => emitted.push(value));
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('input');
  });

  function type(value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  it('has a label', () => {
    expect(fixture.nativeElement.querySelector('mat-label').textContent).toContain('חיפוש');
  });

  it('emits once, 400ms after the last keystroke', fakeAsync(() => {
    type('ד');
    tick(200);
    type('דנ');
    tick(200);
    type('דנה');
    tick(SEARCH_DEBOUNCE_MS - 1);
    expect(emitted).toEqual([]);

    tick(1);
    expect(emitted).toEqual(['דנה']);
  }));

  it('does not emit the same (trimmed) text twice in a row', fakeAsync(() => {
    type('abc');
    tick(SEARCH_DEBOUNCE_MS);
    type('abc ');
    tick(SEARCH_DEBOUNCE_MS);
    type('abcd');
    tick(SEARCH_DEBOUNCE_MS);
    expect(emitted).toEqual(['abc', 'abcd']);
  }));

  it('shows the value from the URL', fakeAsync(() => {
    fixture.componentRef.setInput('value', 'מירי');
    fixture.detectChanges();
    tick(SEARCH_DEBOUNCE_MS);
    expect(input.value).toBe('מירי');
  }));
});
