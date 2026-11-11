import { Component, computed, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { Role } from '../../core/models/auth.models';
import { ALL_AREAS, visibleAreas } from '../../core/navigation/app-areas';
import { Sidebar } from './sidebar';

@Component({ template: '' })
class Blank {}

describe('Sidebar', () => {
  const roles = signal<Role[]>(['Employee']);
  let fixture: ComponentFixture<Sidebar>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Sidebar],
      providers: [
        provideRouter(ALL_AREAS.map((area) => ({ path: area.path, component: Blank }))),
        { provide: AuthService, useValue: { roles: computed(() => roles()) } },
      ],
    });
    fixture = TestBed.createComponent(Sidebar);
  });

  function linkTitles(): string[] {
    const titles = fixture.nativeElement.querySelectorAll('a.nav-item [matListItemTitle]') as NodeListOf<HTMLElement>;
    return Array.from(titles).map((title) => title.textContent!.trim());
  }

  it('shows only the areas of an employee', () => {
    roles.set(['Employee']);
    fixture.detectChanges();

    expect(linkTitles()).toEqual(visibleAreas(['Employee']).map((area) => area.title));
    expect(linkTitles()).not.toContain('תור פניות');
    expect(linkTitles()).not.toContain('ניהול');
  });

  it('shows the union of the areas of a user with several roles, in the fixed order', () => {
    roles.set(['Employee', 'Admin', 'Handler', 'DepartmentManager']);
    fixture.detectChanges();

    const titles = linkTitles();
    expect(titles.slice(0, 4)).toEqual(['הפניות שלי', 'תור פניות', 'דשבורד', 'ניהול']);
  });

  it('marks the active area with aria-current="page"', async () => {
    roles.set(['Employee', 'Handler']);
    fixture.detectChanges();

    await TestBed.inject(Router).navigateByUrl('/handler');
    fixture.detectChanges();
    await fixture.whenStable();

    const current = fixture.nativeElement.querySelectorAll('a[aria-current="page"]') as NodeListOf<HTMLElement>;
    expect(current.length).toBe(1);
    expect(current[0].querySelector('[matListItemTitle]')!.textContent!.trim()).toBe('תור פניות');
  });
});
