import { PagedResult } from '../../../core/models/paging.models';
import { RequesterTicketDetails, RequesterTicketListItem } from '../../../core/models/ticket.models';

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
