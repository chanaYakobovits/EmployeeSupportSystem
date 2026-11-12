import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { Badge } from '../badge/badge';

/** Active / inactive tag of a user or a department. */
@Component({
  selector: 'app-active-badge',
  imports: [Badge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-badge [tone]="active() ? 'success' : 'neutral'">{{ active() ? activeText() : inactiveText() }}</app-badge>`,
})
export class ActiveBadge {
  readonly active = input.required<boolean>();
  readonly activeText = input('פעיל');
  readonly inactiveText = input('לא פעיל');
}
