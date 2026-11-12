import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { skipErrorToast } from '../http/http-context';
import { toHttpParams } from '../http/query-params';
import { Category, CategorySaveResult, CreateCategoryRequest, UpdateCategoryRequest } from '../models/category.models';

/**
 * Typed calls to the backend CategoriesController (spec 9.3). No state.
 * The list is read by every signed-in user; includeInactive and the changes are Admin only.
 * The calling screen shows the errors itself, so no toast.
 */
@Injectable({ providedIn: 'root' })
export class CategoriesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/categories`;

  /** By name, the fallback last; not paged (an organization has a handful of categories). */
  list(includeInactive = false): Observable<Category[]> {
    return this.http.get<Category[]>(this.baseUrl, {
      params: toHttpParams({ includeInactive: includeInactive || null }),
      context: skipErrorToast(),
    });
  }

  create(request: CreateCategoryRequest): Observable<CategorySaveResult> {
    return this.http.post<CategorySaveResult>(this.baseUrl, request, { context: skipErrorToast() });
  }

  update(id: number, request: UpdateCategoryRequest): Observable<CategorySaveResult> {
    return this.http.put<CategorySaveResult>(`${this.baseUrl}/${id}`, request, { context: skipErrorToast() });
  }
}
