import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { DashboardCards } from '../../../models/dashboard.models';
import { formatDuration, formatNumber, formatPercent } from '../dashboard-format';
import { CARD_HINTS, NO_OPENED_IN_RANGE, NO_RESOLVED_IN_RANGE, OVERDUE_ATTENTION } from '../dashboard-messages';

export interface MetricCard {
  id: keyof typeof CARD_HINTS;
  label: string;
  value: string;
  icon: string;
  tone: 'primary' | 'accent' | 'danger' | 'success' | 'neutral';
  /** The definition of the metric (tooltip + screen readers). */
  hint: string;
  /** A short line under the number: the base of a percentage, or why there is no value. */
  note: string | null;
  /** Shown highlighted, with an icon and a text – never by colour alone (spec 11.6). */
  attention: boolean;
}

/** Builds the six cards of spec 7.9 from the server's numbers. Pure. */
export function metricCards(cards: DashboardCards): MetricCard[] {
  const resolvedBase = cards.resolvedInRangeCount > 0 ? `מתוך ${formatNumber(cards.resolvedInRangeCount)} שנפתרו` : NO_RESOLVED_IN_RANGE;
  const openedBase = cards.openedInRangeCount > 0 ? `מתוך ${formatNumber(cards.openedInRangeCount)} שנפתחו` : NO_OPENED_IN_RANGE;
  const overdue = cards.overdueOpenCount > 0;

  return [
    { id: 'open', label: 'פניות פתוחות', value: formatNumber(cards.openCount), icon: 'inbox', tone: 'primary', hint: CARD_HINTS.open, note: 'נכון לעכשיו', attention: false },
    { id: 'newThisWeek', label: 'חדשות השבוע', value: formatNumber(cards.newThisWeekCount), icon: 'fiber_new', tone: 'accent', hint: CARD_HINTS.newThisWeek, note: '7 הימים האחרונים', attention: false },
    {
      id: 'overdue',
      label: 'חריגות SLA פתוחות',
      value: formatNumber(cards.overdueOpenCount),
      icon: overdue ? 'warning' : 'timer',
      tone: overdue ? 'danger' : 'neutral',
      hint: CARD_HINTS.overdue,
      note: overdue ? OVERDUE_ATTENTION : 'אין חריגות כרגע',
      attention: overdue,
    },
    {
      id: 'averageResolution',
      label: 'זמן פתרון ממוצע',
      value: formatDuration(cards.averageResolutionHours),
      icon: 'schedule',
      tone: 'primary',
      hint: CARD_HINTS.averageResolution,
      note: cards.averageResolutionHours === null ? NO_RESOLVED_IN_RANGE : resolvedBase,
      attention: false,
    },
    { id: 'slaCompliance', label: 'עמידה ב-SLA', value: formatPercent(cards.slaCompliancePercent), icon: 'verified', tone: 'success', hint: CARD_HINTS.slaCompliance, note: resolvedBase, attention: false },
    { id: 'autoClassified', label: 'סווגו אוטומטית', value: formatPercent(cards.autoClassifiedPercent), icon: 'auto_awesome', tone: 'accent', hint: CARD_HINTS.autoClassified, note: openedBase, attention: false },
  ];
}

/**
 * The metric cards (spec 7.9, design guide "כרטיסי מדד"): icon in a tinted square, label, big number, a note.
 * Every card has its definition: a tooltip on the (i) button and the same text for screen readers.
 */
@Component({
  selector: 'app-metric-cards',
  imports: [MatButtonModule, MatIconModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './metric-cards.html',
  styleUrl: './metric-cards.scss',
})
export class MetricCards {
  readonly cards = input.required<DashboardCards>();
  protected readonly items = computed(() => metricCards(this.cards()));
}
