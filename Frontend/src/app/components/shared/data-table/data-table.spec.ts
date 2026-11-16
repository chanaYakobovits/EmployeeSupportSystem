import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { DataTable, DataTableColumn, DataTableHeaderCell } from './data-table';

@Component({
  imports: [DataTable, DataTableHeaderCell],
  template: `
    <app-data-table caption="בדיקה" [columns]="columns" [rows]="rows" status="ready" [paginated]="false">
      <ng-template appHeaderCell="pick"><span class="custom-header">בחירה מותאמת</span></ng-template>
    </app-data-table>
  `,
})
class Host {
  readonly columns: DataTableColumn[] = [
    { key: 'pick', header: 'בחירה', sortKey: 'pick' },
    { key: 'name', header: 'שם' },
  ];
  readonly rows = [{ id: 1, name: 'א' }];
}

describe('DataTable', () => {
  it('a header template replaces the header text (and its sort button)', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const headers = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('thead th'));

    expect(headers[0].querySelector('.custom-header')!.textContent).toBe('בחירה מותאמת');
    expect(headers[0].querySelector('button.sort-button')).toBeNull();
    expect(headers[1].textContent!.trim()).toBe('שם');
  });
});
