import { Pipe, PipeTransform } from '@angular/core';

import { RoutingType } from '../../core/models/category.models';

/** Hebrew routing type names (spec 3.5). */
export const ROUTING_TYPE_LABELS: Readonly<Record<RoutingType, string>> = {
  FixedDepartment: 'מחלקה מטפלת קבועה',
  RequesterDepartmentHr: 'HR של מחלקת העובד',
};

export function routingTypeLabel(routingType: RoutingType | null | undefined): string {
  return routingType ? (ROUTING_TYPE_LABELS[routingType] ?? routingType) : '';
}

@Pipe({ name: 'routingTypeLabel' })
export class RoutingTypeLabelPipe implements PipeTransform {
  transform(routingType: RoutingType | null | undefined): string {
    return routingTypeLabel(routingType);
  }
}
