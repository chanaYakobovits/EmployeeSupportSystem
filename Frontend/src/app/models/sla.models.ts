/** Ticket urgency (spec 3.4; Backend Urgency). */
export type Urgency = 'Low' | 'Medium' | 'High' | 'Critical';

/** Most urgent first – the order of the SLA form. */
export const URGENCIES: readonly Urgency[] = ['Critical', 'High', 'Medium', 'Low'];

/** GET / PUT /api/sla-policies: resolution target in calendar hours per urgency (Backend SlaPoliciesDto). */
export interface SlaPolicies {
  critical: number;
  high: number;
  medium: number;
  low: number;
}

/** The SlaPolicies field of an urgency (Critical → critical). */
export type SlaField = keyof SlaPolicies;

export function slaField(urgency: Urgency): SlaField {
  return urgency.toLowerCase() as SlaField;
}

/** Same limit as the server (UpdateSlaPoliciesRequestValidator.MaxHours): one year. */
export const SLA_MAX_HOURS = 8760;
