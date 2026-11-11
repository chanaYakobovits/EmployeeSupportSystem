import { Role } from '../models/auth.models';
import { APP_AREAS, visibleAreas } from './app-areas';

/** The sample users from the README (every user is also an Employee – DatabaseSeeder). */
const README_USERS: { name: string; roles: Role[]; expected: string[] }[] = [
  { name: 'נועה ברקוביץ (Employee)', roles: ['Employee'], expected: ['employee'] },
  { name: 'אלון ביטון (Handler)', roles: ['Employee', 'Handler'], expected: ['employee', 'handler'] },
  { name: 'ליאת שחר (Handler)', roles: ['Employee', 'Handler'], expected: ['employee', 'handler'] },
  { name: 'רון כהנוביץ (DepartmentManager)', roles: ['Employee', 'DepartmentManager'], expected: ['employee', 'dashboard'] },
  { name: 'רינה קפלן (DepartmentManager)', roles: ['Employee', 'DepartmentManager'], expected: ['employee', 'dashboard'] },
  { name: 'אבנר גולדמן (Executive)', roles: ['Employee', 'Executive'], expected: ['employee', 'dashboard'] },
  {
    name: 'יעל הרפז (Admin + Handler + DepartmentManager)',
    roles: ['Employee', 'Admin', 'Handler', 'DepartmentManager'],
    expected: ['employee', 'handler', 'dashboard', 'admin'],
  },
];

describe('visibleAreas (menu by role)', () => {
  for (const user of README_USERS) {
    it(`shows ${user.name}: ${user.expected.join(', ')}`, () => {
      expect(visibleAreas(user.roles, APP_AREAS).map((area) => area.id)).toEqual(user.expected);
    });
  }

  it('keeps the fixed menu order regardless of the order of the roles', () => {
    const roles: Role[] = ['DepartmentManager', 'Admin', 'Handler', 'Employee'];
    expect(visibleAreas(roles, APP_AREAS).map((area) => area.id)).toEqual(['employee', 'handler', 'dashboard', 'admin']);
  });

  it('shows the dashboard once to a user who is both DepartmentManager and Executive', () => {
    const ids = visibleAreas(['Employee', 'DepartmentManager', 'Executive'], APP_AREAS).map((area) => area.id);
    expect(ids).toEqual(['employee', 'dashboard']);
  });

  it('shows nothing without roles', () => {
    expect(visibleAreas([], APP_AREAS)).toEqual([]);
  });
});
