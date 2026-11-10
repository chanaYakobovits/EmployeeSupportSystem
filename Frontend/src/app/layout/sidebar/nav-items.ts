import { DEV_AREA_NAV_ITEMS } from '../../features/dev/dev-area';

export interface NavItem {
  label: string;
  icon: string;
  path: string;
}

/**
 * Static menu for now. Once authentication exists, items will be filtered by the user's roles
 * (spec 7: the sidebar shows only screens the user is allowed to see).
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'הפניות שלי', icon: 'inbox', path: '/employee' },
  { label: 'תור פניות', icon: 'support_agent', path: '/handler' },
  { label: 'דשבורד', icon: 'monitoring', path: '/dashboard' },
  { label: 'ניהול', icon: 'settings', path: '/admin' },
  ...DEV_AREA_NAV_ITEMS,
];
