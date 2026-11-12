import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

import { RequesterTicketHistoryItem } from '../../../../core/models/ticket.models';
import { RelativeTime } from '../../../../shared/components/relative-time/relative-time';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';

/** The ticket's status changes, oldest first (spec 7.5). Comments join it in task 3.5. */
@Component({
  selector: 'app-ticket-timeline',
  imports: [MatIconModule, StatusBadge, RelativeTime],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './ticket-timeline.html',
  styleUrl: './ticket-timeline.scss',
})
export class TicketTimeline {
  readonly items = input.required<readonly RequesterTicketHistoryItem[]>();
}
