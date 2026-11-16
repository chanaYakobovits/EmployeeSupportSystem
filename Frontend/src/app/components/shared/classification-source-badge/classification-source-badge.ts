import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { ClassificationSource } from '../../../models/ticket.models';
import { Badge, BadgeTone } from '../badge/badge';

interface SourceMark {
  label: string;
  tone: BadgeTone;
  title: string;
}

const MARKS: Partial<Record<ClassificationSource, SourceMark>> = {
  Fallback: { label: 'גיבוי', tone: 'warning', title: 'הפנייה לא סווגה אוטומטית ועברה לקטגוריית הגיבוי' },
  Manual: { label: 'ידני', tone: 'neutral', title: 'הסיווג נקבע ידנית על ידי איש צוות' },
};

/** The "סווג ידנית / גיבוי" mark of spec 7.6. Nothing for an automatic (AI) classification or none yet. */
@Component({
  selector: 'app-classification-source-badge',
  imports: [Badge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (mark(); as mark) {
      <app-badge [tone]="mark.tone" [attr.title]="mark.title">
        <span class="visually-hidden">סיווג: </span>{{ mark.label }}
      </app-badge>
    }
  `,
})
export class ClassificationSourceBadge {
  readonly source = input.required<ClassificationSource | null>();
  protected readonly mark = computed(() => {
    const source = this.source();
    return source ? (MARKS[source] ?? null) : null;
  });
}
