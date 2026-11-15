import { TicketAttachment } from '../../../models/attachment.models';
import { PagedResult } from '../../../models/paging.models';
import {
  RequesterTicketDetails,
  RequesterTicketHistory,
  RequesterTicketListItem,
  RequesterTimelineComment,
  RequesterTimelineItem,
} from '../../../models/ticket.models';

export function fakeTicketItem(overrides: Partial<RequesterTicketListItem> = {}): RequesterTicketListItem {
  return {
    id: 1042,
    subject: 'המחשב לא נדלק',
    isSubjectExcerpt: false,
    categoryName: 'מחשוב',
    status: 'InProgress',
    handlerName: 'דנה כהן',
    createdAt: '2026-11-10T08:00:00Z',
    updatedAt: '2026-11-11T09:30:00Z',
    ...overrides,
  };
}

export function pageOf<T>(items: T[]): PagedResult<T> {
  return { items, page: 1, pageSize: 25, totalCount: items.length };
}

export function fakeTicketDetails(overrides: Partial<RequesterTicketDetails> = {}): RequesterTicketDetails {
  return {
    id: 1042,
    subject: 'המחשב לא נדלק',
    description: 'הבוקר המחשב לא נדלק.\nניסיתי להחליף שקע.',
    status: 'New',
    categoryName: null,
    handlerName: null,
    createdAt: '2026-11-10T08:00:00Z',
    updatedAt: '2026-11-10T08:00:00Z',
    resolvedAt: null,
    closedAt: null,
    timeline: [{ previousStatus: null, newStatus: 'New', changedAt: '2026-11-10T08:00:00Z', changedByName: 'ישראל ישראלי' }],
    ...overrides,
  };
}

export function fakeAttachment(overrides: Partial<TicketAttachment> = {}): TicketAttachment {
  return {
    id: 7,
    fileName: 'צילום מסך.png',
    contentType: 'image/png',
    sizeBytes: 350 * 1024,
    uploadedByName: 'ישראל ישראלי',
    createdAt: '2026-11-10T08:00:00Z',
    ...overrides,
  };
}

export function fakeComment(overrides: Partial<RequesterTimelineComment> = {}): RequesterTimelineComment {
  return {
    kind: 'comment',
    at: '2026-11-10T09:00:00Z',
    id: 1,
    authorName: 'דנה כהן',
    isFromRequester: false,
    body: 'אפשר לצרף צילום מסך?',
    isResolution: false,
    attachments: [],
    ...overrides,
  };
}

export function fakeHistory(items?: RequesterTimelineItem[], ticketId = 1042): RequesterTicketHistory {
  return {
    ticketId,
    items: items ?? [
      { kind: 'status', at: '2026-11-10T08:00:00Z', previousStatus: null, newStatus: 'New', changedByName: 'ישראל ישראלי' },
    ],
  };
}

/** A chosen file of this size (bytes). Same name and size → the same file for duplicate checks. */
export function fakeFile(name: string, size = 1024): File {
  return new File([new Uint8Array(size)], name, { lastModified: 1_700_000_000_000 });
}
