import { safeQueueUrl, staffTicketUrl } from './handler-urls';

describe('handler-urls', () => {
  it('safeQueueUrl: a URL of the area itself, never another ticket or another area', () => {
    expect(safeQueueUrl({ queueUrl: '/handler/queue?page=2' })).toBe('/handler/queue?page=2');
    expect(safeQueueUrl({ queueUrl: '/handler/tickets/5' })).toBe('/handler');
    expect(safeQueueUrl({ queueUrl: 'https://evil.example/handler/' })).toBe('/handler');
    expect(safeQueueUrl({ queueUrl: '/dashboard/organization' })).toBe('/handler');
    expect(safeQueueUrl(null)).toBe('/handler');

    expect(safeQueueUrl({ queueUrl: '/dashboard/departments/4?view=handling' }, 'dashboard')).toBe(
      '/dashboard/departments/4?view=handling',
    );
    expect(safeQueueUrl({ queueUrl: '/dashboard/tickets/9' }, 'dashboard')).toBe('/dashboard');
    expect(safeQueueUrl({ queueUrl: '/handler/queue' }, 'dashboard')).toBe('/dashboard');
    expect(safeQueueUrl({}, 'dashboard')).toBe('/dashboard');
  });

  it('staffTicketUrl: a Handler in the handler area, a manager / executive in the dashboard area', () => {
    expect(staffTicketUrl(7, ['Employee', 'Handler', 'DepartmentManager'])).toBe('/handler/tickets/7');
    expect(staffTicketUrl(7, ['Employee', 'DepartmentManager'])).toBe('/dashboard/tickets/7');
    expect(staffTicketUrl(7, ['Employee', 'Executive'])).toBe('/dashboard/tickets/7');
    expect(staffTicketUrl(7, ['Employee', 'Admin'])).toBeNull();
  });
});
