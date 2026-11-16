import { TestBed } from '@angular/core/testing';

import { Urgency } from '../../../models/sla.models';
import { ClassificationSource } from '../../../models/ticket.models';
import { ClassificationSourceBadge } from '../classification-source-badge/classification-source-badge';
import { UrgencyBadge } from './urgency-badge';

describe('UrgencyBadge', () => {
  function render(urgency: Urgency | null): HTMLElement {
    const fixture = TestBed.createComponent(UrgencyBadge);
    fixture.componentRef.setInput('urgency', urgency);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  it('a dot and the Hebrew text – never the colour alone', () => {
    const element = render('Critical');
    expect(element.textContent).toContain('קריטית');
    expect(element.querySelector('.dot')).not.toBeNull();
    expect(element.classList).toContain('urgency-critical');
  });

  it('no urgency yet: "לא נקבעה"', () => {
    const element = render(null);
    expect(element.textContent).toContain('לא נקבעה');
    expect(element.classList).toContain('urgency-none');
  });
});

describe('ClassificationSourceBadge', () => {
  function render(source: ClassificationSource | null): HTMLElement {
    const fixture = TestBed.createComponent(ClassificationSourceBadge);
    fixture.componentRef.setInput('source', source);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  it('"גיבוי" for the fallback, "ידני" for a manual classification, nothing for AI', () => {
    expect(render('Fallback').textContent).toContain('גיבוי');
    expect(render('Manual').textContent).toContain('ידני');
    expect(render('Ai').textContent!.trim()).toBe('');
    expect(render(null).textContent!.trim()).toBe('');
  });
});
