import { TestBed } from '@angular/core/testing';

import { TICKET_STATUS_VALUES } from '../../../models/ticket.models';
import { TICKET_STATUS_COLOR_VARS, cssColor } from '../../../services/status-colors';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  it('every status takes its colour from the shared source (the same as the dashboard’s status chart)', () => {
    for (const status of TICKET_STATUS_VALUES) {
      const fixture = TestBed.createComponent(StatusBadge);
      fixture.componentRef.setInput('status', status);
      fixture.detectChanges();
      const host = fixture.nativeElement as HTMLElement;

      expect(host.style.getPropertyValue('--status-color')).toBe(`var(${TICKET_STATUS_COLOR_VARS[status]})`);
      expect(getComputedStyle(host).color).toBe(toRgb(cssColor(TICKET_STATUS_COLOR_VARS[status])));
    }
  });
});

/** The computed colour of a CSS colour value, as the browser writes it (rgb(...)). */
function toRgb(value: string): string {
  const probe = document.createElement('span');
  probe.style.color = value;
  document.body.appendChild(probe);
  const rgb = getComputedStyle(probe).color;
  probe.remove();
  return rgb;
}
