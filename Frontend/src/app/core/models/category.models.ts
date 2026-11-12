import { DepartmentSummary } from './department.models';

/** How a category is routed (spec 3.5; Backend RoutingType). */
export type RoutingType = 'FixedDepartment' | 'RequesterDepartmentHr';

export const ROUTING_TYPES: readonly RoutingType[] = ['FixedDepartment', 'RequesterDepartmentHr'];

/** GET /api/categories (Backend CategoryDto). handlingDepartment is null for RequesterDepartmentHr. */
export interface Category {
  id: number;
  code: string;
  name: string;
  description: string;
  routingType: RoutingType;
  handlingDepartment: DepartmentSummary | null;
  /** The fallback category (GENERAL): always active, exactly one. */
  isFallback: boolean;
  isActive: boolean;
}

/** POST /api/categories. A new category is always active and never the fallback. */
export interface CreateCategoryRequest {
  code: string;
  name: string;
  description: string;
  routingType: RoutingType;
  handlingDepartmentId: number | null;
}

/** PUT /api/categories/{id}. The code cannot be changed, so it is not sent. */
export interface UpdateCategoryRequest {
  name: string;
  description: string;
  routingType: RoutingType;
  handlingDepartmentId: number | null;
  isActive: boolean;
}

/** Response of POST / PUT: the saved category plus warnings that did not block the save. */
export interface CategorySaveResult {
  category: Category;
  warnings: string[];
}

/** Same limits as the server (CategoryFieldsValidator / CategoryConfiguration). */
export const CATEGORY_LIMITS = { codeMaxLength: 30, nameMaxLength: 100, descriptionMaxLength: 500 } as const;

/** Upper-case English letters and underscores, starting with a letter, 2–30 characters (as on the server). */
export const CATEGORY_CODE_PATTERN = /^[A-Z][A-Z_]{1,29}$/;
