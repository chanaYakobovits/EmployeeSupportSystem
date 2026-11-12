import { ChangeDetectionStrategy, Component, effect, input, untracked } from '@angular/core';
import { outputFromObservable } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';

/** Wait this long after the last keystroke before searching (spec 10.4). */
export const SEARCH_DEBOUNCE_MS = 400;

/**
 * Search box of a list's filter bar. Emits the trimmed text 400ms after the user stops typing, only when it changed;
 * the list's store then loads with switchMap, so the previous request is cancelled (spec 10.4).
 * value: the search currently in the URL (e.g. after refresh, or cleared by "clear filters").
 */
@Component({
  selector: 'app-search-field',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatIconModule, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search-field">
      <mat-label>{{ label() }}</mat-label>
      <mat-icon matPrefix aria-hidden="true">search</mat-icon>
      <input matInput type="search" dir="auto" autocomplete="off" [formControl]="control" [placeholder]="placeholder()" />
      @if (control.value) {
        <button mat-icon-button matSuffix type="button" aria-label="ניקוי החיפוש" (click)="control.setValue('')">
          <mat-icon aria-hidden="true">close</mat-icon>
        </button>
      }
    </mat-form-field>
  `,
  styles: `
    :host {
      display: block;
    }
    .search-field {
      width: 100%;
    }
  `,
})
export class SearchField {
  readonly label = input('חיפוש');
  readonly placeholder = input('');
  readonly value = input<string | null>(null);

  protected readonly control = new FormControl('', { nonNullable: true });

  readonly search = outputFromObservable(
    this.control.valueChanges.pipe(
      map((text) => text.trim()),
      debounceTime(SEARCH_DEBOUNCE_MS),
      distinctUntilChanged(),
    ),
  );

  constructor() {
    // Follow the URL. The change goes through the same pipeline; the store ignores a search equal to the current one.
    effect(() => {
      const external = this.value() ?? '';
      untracked(() => {
        if (this.control.value.trim() !== external) {
          this.control.setValue(external);
        }
      });
    });
  }
}

