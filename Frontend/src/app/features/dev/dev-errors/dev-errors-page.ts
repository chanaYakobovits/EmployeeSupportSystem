import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { DevErrorKind } from '../../../core/api/dev-errors-api.service';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';
import { DevErrorsStore } from './dev-errors.store';

interface DevErrorButton {
  kind: DevErrorKind;
  label: string;
  expected: string;
}

/** TEMPORARY – development only. One button per DevErrorsController endpoint. */
@Component({
  selector: 'app-dev-errors-page',
  imports: [JsonPipe, MatButtonModule, MatCardModule, LoadingState],
  providers: [DevErrorsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dev-errors-page.html',
  styleUrl: './dev-errors-page.scss',
})
export class DevErrorsPage {
  protected readonly store = inject(DevErrorsStore);

  protected readonly buttons: readonly DevErrorButton[] = [
    { kind: 'not-found', label: 'לא נמצא', expected: '404' },
    { kind: 'conflict', label: 'התנגשות', expected: '409' },
    { kind: 'business-rule', label: 'כלל עסקי', expected: '422' },
    { kind: 'model-validation', label: 'קלט לא תקין', expected: '400' },
    { kind: 'unexpected', label: 'שגיאה לא צפויה', expected: '500' },
    { kind: 'network', label: 'שרת לא זמין', expected: '0' },
  ];
}
