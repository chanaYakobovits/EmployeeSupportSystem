import { toHttpParams } from './query-params';

describe('toHttpParams', () => {
  it('leaves out empty values', () => {
    expect(toHttpParams({ a: null, b: undefined, c: '', d: [], e: 0, f: false }).toString()).toBe('e=0&f=false');
  });

  it('sends an array as a repeated parameter', () => {
    expect(toHttpParams({ status: ['New', 'Assigned'], page: 1 }).toString()).toBe('status=New&status=Assigned&page=1');
  });
});
