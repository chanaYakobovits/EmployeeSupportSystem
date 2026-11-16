import { isStaffTicketDetails, TicketDetails } from './ticket.models';

describe('isStaffTicketDetails', () => {
  const requesterView = {
    id: 1,
    subject: null,
    description: 'תיאור',
    status: 'Resolved',
    categoryName: null,
    handlerName: null,
    createdAt: '2026-11-01T08:00:00Z',
    updatedAt: '2026-11-01T08:00:00Z',
    resolvedAt: null,
    closedAt: null,
    timeline: [],
    // Since task 3.5 the requester's view has these too.
    rowVersion: 'AAAAAAAAB9E=',
    allowedActions: ['reply', 'confirmResolved', 'reopen'],
  } as unknown as TicketDetails;

  it('treats the requester view with actions as the requester view', () => {
    expect(isStaffTicketDetails(requesterView)).toBeFalse();
  });

  it('recognizes the staff view by its requester object', () => {
    const staffView = {
      ...requesterView,
      requester: { id: 2, name: 'נועה', departmentName: 'פיתוח', jobTitle: 'מפתחת' },
    } as unknown as TicketDetails;

    expect(isStaffTicketDetails(staffView)).toBeTrue();
  });
});
