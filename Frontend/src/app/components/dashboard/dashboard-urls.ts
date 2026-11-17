/** The dashboard area's URLs (spec 7.9). */
export const DASHBOARD_HOME_URL = '/dashboard';

export const ORGANIZATION_DASHBOARD_URL = `${DASHBOARD_HOME_URL}/organization`;

export function departmentDashboardUrl(id: number): string {
  return `${DASHBOARD_HOME_URL}/departments/${id}`;
}

/** The handling screen of a ticket opened from a dashboard (a manager / executive may not have the Handler role). */
export function dashboardTicketUrl(id: number): string {
  return `${DASHBOARD_HOME_URL}/tickets/${id}`;
}
