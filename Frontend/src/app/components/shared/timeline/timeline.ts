import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import {
  RESOLVED_WINDOW_DAYS,
  RequesterTimelineStatus,
  StaffTimelineChange,
  TimelineItem,
} from '../../../models/ticket.models';
import { statusLabel } from '../../../pipes/status-label.pipe';
import { urgencyLabel } from '../../../pipes/urgency-label.pipe';
import { AttachmentChip } from '../attachment-chip/attachment-chip';
import { RelativeTime } from '../relative-time/relative-time';
import { StatusBadge } from '../status-badge/status-badge';

/** One "from → to" line of a reclassification or a transfer. */
export interface ChangeLine {
  label: string;
  from: string;
  to: string;
}

const CHANGE_TITLES: Record<StaffTimelineChange['changeKind'], string> = {
  Reclassified: 'הסיווג שונה',
  Transferred: 'הפנייה הועברה',
  Taken: 'הפנייה נלקחה לטיפול',
};

/** The lines of what changed – only what actually changed. A null handler is the department's queue. */
export function changeLines(change: StaffTimelineChange): ChangeLine[] {
  const lines: ChangeLine[] = [];
  const add = (label: string, from: string | null, to: string | null, empty: string) => {
    if (from !== to) {
      lines.push({ label, from: from ?? empty, to: to ?? empty });
    }
  };
  add('קטגוריה', change.fromCategoryName, change.toCategoryName, 'ללא');
  add(
    'דחיפות',
    change.fromUrgency ? urgencyLabel(change.fromUrgency) : null,
    change.toUrgency ? urgencyLabel(change.toUrgency) : null,
    'לא נקבעה',
  );
  add('מחלקה מטפלת', change.fromDepartmentName, change.toDepartmentName, 'ללא');
  add('מטפל', change.fromHandlerName, change.toHandlerName, 'תור המחלקה');
  return lines;
}

/**
 * A status change the system made (changedByName null), in words – the server's automatic moves (spec 3.3):
 * routing, back to handling after the requester's reply, the automatic close, a removed handler. Null for a person's
 * change and for the opening entry.
 */
export function systemEventText(item: RequesterTimelineStatus, perspective: 'requester' | 'staff'): string | null {
  if (item.changedByName !== null || item.previousStatus === null) {
    return null;
  }
  const move = `${item.previousStatus}>${item.newStatus}`;
  switch (move) {
    case 'New>Assigned':
      return 'הפנייה שויכה לטיפול';
    case 'WaitingForEmployee>InProgress':
      return perspective === 'requester'
        ? 'הפנייה חזרה לטיפול אחרי התגובה שלך'
        : 'הפנייה חזרה לטיפול אחרי תגובת העובד';
    case 'Resolved>Closed':
      return `הפנייה נסגרה אוטומטית ${RESOLVED_WINDOW_DAYS} ימים אחרי הפתרון`;
    case 'InProgress>Assigned':
      return 'המטפל הוחלף – הפנייה ממתינה לתחילת טיפול';
    default:
      return `הסטטוס שונה ל-${statusLabel(item.newStatus)}`;
  }
}

/**
 * One timeline of a ticket, oldest first (spec 7.5, 7.7): status changes, comments and files – and for staff also
 * internal notes (yellow, "פנימי"), the reasons of status changes and the reclassifications / transfers with their
 * reasons. Shows exactly what the server returned – nothing is hidden or added here: the requester's history never
 * contains internal notes or reasons. A comment's text is plain text (interpolation, never innerHTML), line breaks kept.
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
  readonly items = input.required<readonly TimelineItem[]>();
  /** Who is reading: the requester ("תגובה שלך" for their own comments) or staff (the requester is "העובד"). */
  readonly perspective = input<'requester' | 'staff'>('requester');

  protected readonly changeTitles = CHANGE_TITLES;
  protected readonly changeLines = changeLines;

  protected trackKey(item: TimelineItem): string {
    switch (item.kind) {
      case 'comment':
        return `comment:${item.id}`;
      case 'attachment':
        return `attachment:${item.attachment.id}`;
      case 'change':
        return `change:${item.at}:${item.changeKind}`;
      default:
        return `status:${item.at}:${item.newStatus}`;
    }
  }

  /** Only the staff history has these fields. */
  protected isInternal(item: TimelineItem): boolean {
    return item.kind === 'comment' && 'isInternal' in item && item.isInternal;
  }

  protected reason(item: TimelineItem): string | null {
    return item.kind === 'status' && 'reason' in item ? item.reason : null;
  }

  /** What the system did, in words (null for a person's change and for the opening entry). */
  protected systemEventText(item: TimelineItem): string | null {
    return item.kind === 'status' ? systemEventText(item, this.perspective()) : null;
  }

  protected commentLabel(authorName: string, isFromRequester: boolean, internal: boolean, resolution = false): string {
    if (internal) {
      return `הערה פנימית מ-${authorName}`;
    }
    if (isFromRequester) {
      return this.perspective() === 'requester' ? 'תגובה שלך' : `תגובה מהעובד ${authorName}`;
    }
    return resolution ? `תגובת פתרון מ-${authorName}` : `תגובה מ-${authorName}`;
  }
}
