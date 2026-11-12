import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { filter, switchMap } from 'rxjs';

import { Category, CategorySaveResult } from '../../../../core/models/category.models';
import { NotificationService } from '../../../../core/notifications/notification.service';
import { ActiveBadge } from '../../../../shared/components/active-badge/active-badge';
import { Badge } from '../../../../shared/components/badge/badge';
import { ConfirmService } from '../../../../shared/components/confirm-dialog/confirm.service';
import { DataTable, DataTableCell, DataTableColumn } from '../../../../shared/components/data-table/data-table';
import { EmptyState } from '../../../../shared/components/empty-state/empty-state';
import { FormAlert, FormAlertBox } from '../../../../shared/components/form-alert/form-alert';
import { PageHeader } from '../../../../shared/components/page-header/page-header';
import { RoutingTypeLabelPipe } from '../../../../shared/pipes/routing-type-label.pipe';
import { adminAlert } from '../../admin-errors';
import { CategoriesStore } from '../categories.store';
import { openCategoryDialog } from '../category-dialog/category-dialog';

/**
 * Categories and routing (spec 7.8): code, name, routing type, handling department, active.
 * Create / edit in a dialog; no delete – a category is deactivated. The fallback (GENERAL) is always active.
 */
@Component({
  selector: 'app-categories-page',
  imports: [
    MatButtonModule,
    MatIconModule,
    PageHeader,
    DataTable,
    DataTableCell,
    EmptyState,
    ActiveBadge,
    Badge,
    FormAlertBox,
    RoutingTypeLabelPipe,
  ],
  providers: [CategoriesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './categories-page.html',
  styleUrl: './categories-page.scss',
})
export class CategoriesPage {
  protected readonly store = inject(CategoriesStore);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  /** Warnings of the last save (e.g. no active handlers): they did not block it, so they stay until dismissed. */
  protected readonly warning = signal<FormAlert | null>(null);

  protected readonly columns: readonly DataTableColumn[] = [
    { key: 'code', header: 'קוד' },
    { key: 'name', header: 'שם' },
    { key: 'routingType', header: 'סוג ניתוב' },
    { key: 'handlingDepartment', header: 'מחלקה מטפלת' },
    { key: 'isActive', header: 'סטטוס' },
    { key: 'actions', header: 'פעולות', align: 'end' },
  ];

  constructor() {
    this.store.load();
  }

  protected openDialog(category: Category | null): void {
    openCategoryDialog(this.dialog, category)
      .afterClosed()
      .pipe(filter((result): result is CategorySaveResult => !!result))
      .subscribe((result) => {
        this.store.upsert(result.category);
        this.showWarnings(result);
        this.notifications.success(category ? 'הקטגוריה עודכנה' : 'הקטגוריה נוספה');
      });
  }

  protected toggleActive(category: Category): void {
    const deactivate = category.isActive;
    this.confirm
      .confirm(
        deactivate
          ? {
              heading: 'השבתת קטגוריה',
              message: `הקטגוריה ${category.name} תסומן כלא פעילה.`,
              consequences: [
                'פניות קיימות ישמרו על הקטגוריה.',
                'היא לא תוצע יותר ל-AI ולא לסיווג ידני של פניות.',
                'הקטגוריה לא תימחק, ואפשר להפעיל אותה מחדש.',
              ],
              confirmText: 'השבתה',
              tone: 'danger',
            }
          : {
              heading: 'הפעלת קטגוריה',
              message: `הקטגוריה ${category.name} תחזור להיות פעילה ותוצע שוב לסיווג.`,
              confirmText: 'הפעלה',
            },
      )
      .pipe(
        filter(Boolean),
        switchMap(() => this.store.setActive(category, !deactivate)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (result) => {
          this.showWarnings(result);
          this.notifications.success(deactivate ? 'הקטגוריה הושבתה' : 'הקטגוריה הופעלה');
        },
        error: (error: unknown) => {
          const alert = adminAlert(error);
          this.notifications.error(alert.detail ? `${alert.title}. ${alert.detail}` : alert.title);
        },
      });
  }

  private showWarnings(result: CategorySaveResult): void {
    this.warning.set(
      result.warnings.length > 0
        ? {
            tone: 'warning',
            title: `הקטגוריה ${result.category.name} נשמרה, אבל:`,
            detail: result.warnings.join(' '),
          }
        : null,
    );
  }
}
