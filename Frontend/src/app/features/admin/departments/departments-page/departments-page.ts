import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';

import { Department } from '../../../../core/models/department.models';
import { fullName } from '../../../../core/models/user.models';
import { NotificationService } from '../../../../core/notifications/notification.service';
import { ActiveBadge } from '../../../../shared/components/active-badge/active-badge';
import { ConfirmService } from '../../../../shared/components/confirm-dialog/confirm.service';
import { DataTable, DataTableCell, DataTableColumn } from '../../../../shared/components/data-table/data-table';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { USERS_URL, adminAlert } from '../../admin-errors';
import { openDepartmentDialog } from '../department-dialog/department-dialog';
import { DepartmentsStore } from '../departments.store';

/** Departments (spec 7.8): name, manager, active employees and handlers; create / edit in a dialog. */
@Component({
  selector: 'app-departments-page',
  imports: [RouterLink, MatButtonModule, MatIconModule, PageHeader, DataTable, DataTableCell, EmptyState, ActiveBadge],
  providers: [DepartmentsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './departments-page.html',
  styleUrl: './departments-page.scss',
})
export class DepartmentsPage {
  protected readonly store = inject(DepartmentsStore);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly usersUrl = USERS_URL;
  protected readonly fullName = fullName;
  protected readonly columns: readonly DataTableColumn[] = [
    { key: 'name', header: 'שם המחלקה' },
    { key: 'manager', header: 'מנהל/ת' },
    { key: 'activeUserCount', header: 'עובדים פעילים', align: 'center' },
    { key: 'handlerCount', header: 'גורמים מטפלים', align: 'center' },
    { key: 'isActive', header: 'סטטוס' },
    { key: 'actions', header: 'פעולות', align: 'end' },
  ];

  constructor() {
    this.store.load();
  }

  protected openDialog(department: Department | null): void {
    openDepartmentDialog(this.dialog, department)
      .afterClosed()
      .pipe(filter((saved): saved is Department => !!saved))
      .subscribe((saved) => {
        this.store.upsert(saved);
        this.notifications.success(department ? 'המחלקה עודכנה' : 'המחלקה נוספה');
      });
  }

  protected toggleActive(department: Department): void {
    const deactivate = department.isActive;
    this.confirm
      .confirm(
        deactivate
          ? {
              heading: 'השבתת מחלקה',
              message: `מחלקת ${department.name} תסומן כלא פעילה.`,
              consequences: [
                'לא ניתן יהיה לשייך אליה עובדים חדשים.',
                'אפשר להשבית רק מחלקה שאין בה עובדים פעילים ושאינה מטפלת בקטגוריה פעילה.',
                'המחלקה לא תימחק, ואפשר להפעיל אותה מחדש.',
              ],
              confirmText: 'השבתה',
              tone: 'danger',
            }
          : {
              heading: 'הפעלת מחלקה',
              message: `מחלקת ${department.name} תחזור להיות פעילה.`,
              confirmText: 'הפעלה',
            },
      )
      .pipe(
        filter(Boolean),
        switchMap(() => this.store.setActive(department, !deactivate)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => this.notifications.success(deactivate ? 'המחלקה הושבתה' : 'המחלקה הופעלה'),
        error: (error: unknown) => {
          const alert = adminAlert(error);
          this.notifications.error(alert.detail ? `${alert.title}. ${alert.detail}` : alert.title);
        },
      });
  }
}
