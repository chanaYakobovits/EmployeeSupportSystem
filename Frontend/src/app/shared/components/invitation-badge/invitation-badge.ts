import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { InvitationStatus } from '../../../core/models/user.models';
import { Badge, BadgeTone } from '../badge/badge';

/** Hebrew label and colour of each invitation state (users list, user form, the status filter). */
export const INVITATION_STATUSES: Readonly<Record<InvitationStatus, { label: string; tone: BadgeTone }>> = {
  Pending: { label: 'ממתינה', tone: 'accent' },
  Sent: { label: 'נשלחה', tone: 'primary' },
  Used: { label: 'נוצלה', tone: 'success' },
  Expired: { label: 'פגה', tone: 'warning' },
  Failed: { label: 'נכשלה', tone: 'danger' },
  Revoked: { label: 'בוטלה', tone: 'neutral' },
};

/** Filter order: the states that need the admin first. */
export const INVITATION_STATUS_ORDER: readonly InvitationStatus[] = ['Failed', 'Expired', 'Pending', 'Sent', 'Revoked', 'Used'];

/** "Pill" tag of an invitation state; "—" for a user who was never invited. */
@Component({
  selector: 'app-invitation-badge',
  imports: [Badge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (info(); as info) {
      <app-badge [tone]="info.tone">{{ info.label }}</app-badge>
    } @else {
      <span class="none" aria-label="לא נשלחה הזמנה">—</span>
    }
  `,
  styles: `
    .none {
      color: var(--text-secondary);
    }
  `,
})
export class InvitationBadge {
  readonly status = input<InvitationStatus | null | undefined>(null);

  protected readonly info = computed(() => {
    const status = this.status();
    return status ? INVITATION_STATUSES[status] : null;
  });
}
