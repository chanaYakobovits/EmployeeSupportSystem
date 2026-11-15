import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import { RequesterTimelineItem } from '../../../models/ticket.models';
import { AttachmentChip } from '../attachment-chip/attachment-chip';
import { RelativeTime } from '../relative-time/relative-time';
import { StatusBadge } from '../status-badge/status-badge';

/**
 * One timeline of a ticket, oldest first (spec 7.5): status changes, comments and files, as an ordered list.
 * Shows exactly what the server returned – nothing is hidden here (internal notes are never sent to the requester).
 * A comment's text is plain text (interpolation, never innerHTML), line breaks kept, links not converted.
 */
@Component({
  selector: 'app-timeline',
  imports: [MatIconModule, StatusBadge, RelativeTime, AttachmentChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './timeline.html',
  styleUrl: './timeline.scss',
})
export class Timeline {
  readonly ticketId = input.required<number>();
  readonly items = input.required<readonly RequesterTimelineItem[]>();

  protected trackKey(item: RequesterTimelineItem): string {
    switch (item.kind) {
      case 'comment':
        return `comment:${item.id}`;
      case 'attachment':
        return `attachment:${item.attachment.id}`;
      default:
        return `status:${item.at}:${item.newStatus}`;
    }
  }
}
