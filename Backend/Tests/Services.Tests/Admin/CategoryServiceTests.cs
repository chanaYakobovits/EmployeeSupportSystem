using Common.Constants;
using Common.DTOs.Categories;
using Common.DTOs.Departments;
using Common.Enums;
using Common.Exceptions;
using Repositories.Interfaces;
using Services.Implementations;
using Services.Validators;

namespace Services.Tests.Admin;

public sealed class CategoryServiceTests : AdminTestBase
{
    private Task<CategorySaveResult> Create(string code = "HOME_OFFICE", RoutingType? routingType = RoutingType.FixedDepartment,
        int? handlingDepartmentId = DevelopmentId, string name = "עבודה מהבית", string description = "ציוד וזכאות לעבודה מהבית") =>
        CategoryService.CreateAsync(new CreateCategoryRequest(code, name, description, routingType, handlingDepartmentId),
            Ip, CancellationToken.None);

    /// <summary>Updates with the category's current values, except what is given.</summary>
    private Task<CategorySaveResult> Update(int categoryId, string? name = null, RoutingType? routingType = null,
        int? handlingDepartmentId = -1, bool? isActive = null)
    {
        var category = Categories.Categories[categoryId];
        return CategoryService.UpdateAsync(categoryId,
            new UpdateCategoryRequest(name ?? category.Name, category.Description, routingType ?? category.RoutingType,
                handlingDepartmentId == -1 ? category.HandlingDepartmentId : handlingDepartmentId,
                isActive ?? category.IsActive),
            Ip, CancellationToken.None);
    }

    private static async Task<IReadOnlyDictionary<string, string[]>> FieldErrors(Func<Task> action) =>
        (await Assert.ThrowsAsync<ValidationException>(action)).Errors;

    // ===================== Code =====================

    [Fact]
    public async Task A_new_category_is_active_not_the_fallback_and_audited()
    {
        var result = await Create();

        Assert.Equal("HOME_OFFICE", result.Category.Code);
        Assert.True(result.Category.IsActive);
        Assert.False(result.Category.IsFallback);
        Assert.Equal(DevelopmentId, result.Category.HandlingDepartment?.Id);
        Assert.Empty(result.Warnings);
        var audit = Assert.Single(AuditLog.Entries);
        Assert.Equal((AuditActions.CategoryCreated, AuditEntityTypes.Category, result.Category.Id),
            (audit.Action, audit.EntityType, audit.EntityId!.Value));
    }

    [Theory]
    [InlineData("payroll")]
    [InlineData("HOME-OFFICE")]
    [InlineData("_HOME")]
    [InlineData("שכר")]
    [InlineData("A")]
    [InlineData("HOME1")]
    [InlineData("")]
    public async Task An_invalid_code_is_a_field_error(string code)
    {
        var errors = await FieldErrors(() => Create(code: code));

        Assert.True(errors.ContainsKey("code"));
    }

    [Fact]
    public async Task A_taken_code_is_a_conflict_on_the_code_field()
    {
        var exception = await Assert.ThrowsAsync<ConflictException>(() => Create(code: "PAYROLL"));

        Assert.Equal([CategoryService.DuplicateCodeMessage], exception.Errors["code"]);
    }

    [Fact]
    public async Task A_concurrent_create_with_the_same_code_is_the_same_conflict()
    {
        UnitOfWork.ThrowOnNextSave = new UniqueConstraintException(UniqueIndexes.CategoryCode);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => Create());

        Assert.True(exception.Errors.ContainsKey("code"));
    }

    [Fact]
    public async Task Update_never_changes_the_code()
    {
        // UpdateCategoryRequest has no Code: there is nothing to send.
        Assert.DoesNotContain(typeof(UpdateCategoryRequest).GetProperties(), p => p.Name is "Code" or "IsFallback");

        var result = await Update(PayrollId, name: "שכר");

        Assert.Equal("PAYROLL", result.Category.Code);
        Assert.Equal("שכר", result.Category.Name);
    }

    // ===================== Fallback (GENERAL) =====================

    [Fact]
    public async Task The_fallback_category_cannot_be_deactivated()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Update(GeneralId, isActive: false));

        Assert.Equal("fallback-category-deactivation", exception.ErrorType);
        Assert.True(Categories.Categories[GeneralId].IsActive);
        Assert.Empty(AuditLog.Entries);
    }

    [Fact]
    public async Task The_fallback_category_can_be_renamed_and_stays_the_fallback()
    {
        var result = await Update(GeneralId, name: "כללי – תור גיבוי");

        Assert.True(result.Category.IsFallback);
        Assert.True(result.Category.IsActive);
        Assert.Single(Categories.Categories.Values, c => c.IsFallback);
    }

    [Fact]
    public async Task Creating_a_category_never_moves_the_fallback_flag()
    {
        await Create();

        Assert.Equal(GeneralId, Assert.Single(Categories.Categories.Values, c => c.IsFallback).Id);
    }

    // ===================== Routing and handling department =====================

    [Fact]
    public async Task FixedDepartment_requires_a_handling_department()
    {
        var errors = await FieldErrors(() => Create(handlingDepartmentId: null));

        Assert.Equal([CategoryFieldsValidator.HandlingDepartmentRequiredMessage], errors["handlingDepartmentId"]);
    }

    [Theory]
    [InlineData(InactiveDepartmentId)]
    [InlineData(12345)]
    public async Task FixedDepartment_requires_an_active_existing_department(int departmentId)
    {
        var errors = await FieldErrors(() => Create(handlingDepartmentId: departmentId));

        Assert.Equal([CategoryService.InactiveDepartmentMessage], errors["handlingDepartmentId"]);
    }

    [Fact]
    public async Task RequesterDepartmentHr_must_not_have_a_handling_department()
    {
        var errors = await FieldErrors(() => Create(routingType: RoutingType.RequesterDepartmentHr, handlingDepartmentId: HrId));

        Assert.Equal([CategoryFieldsValidator.HandlingDepartmentNotAllowedMessage], errors["handlingDepartmentId"]);
    }

    [Fact]
    public async Task Switching_to_RequesterDepartmentHr_clears_the_handling_department()
    {
        var result = await Update(PayrollId, routingType: RoutingType.RequesterDepartmentHr, handlingDepartmentId: null);

        Assert.Equal(RoutingType.RequesterDepartmentHr, result.Category.RoutingType);
        Assert.Null(result.Category.HandlingDepartment);
        Assert.Null(Categories.Categories[PayrollId].HandlingDepartmentId);
    }

    [Fact]
    public async Task Missing_routing_type_is_a_field_error()
    {
        var errors = await FieldErrors(() => Create(routingType: null));

        Assert.True(errors.ContainsKey("routingType"));
    }

    [Fact]
    public async Task A_department_without_active_handlers_is_saved_with_a_warning()
    {
        // Management has only the CEO – no handlers.
        var result = await Create(handlingDepartmentId: ManagementId);

        Assert.Equal(ManagementId, result.Category.HandlingDepartment?.Id);
        Assert.Equal([CategoryService.NoActiveHandlersWarning("הנהלה")], result.Warnings);
    }

    [Fact]
    public async Task RequesterDepartmentHr_has_no_warning()
    {
        var result = await Create(routingType: RoutingType.RequesterDepartmentHr, handlingDepartmentId: null);

        Assert.Empty(result.Warnings);
    }

    // ===================== Deactivation =====================

    [Fact]
    public async Task Deactivating_keeps_the_category_and_is_audited()
    {
        var result = await Update(PayrollId, isActive: false);

        Assert.False(result.Category.IsActive);
        Assert.True(Categories.Categories.ContainsKey(PayrollId));
        Assert.Equal([AuditActions.CategoryUpdated, AuditActions.CategoryDeactivated], AuditLog.Entries.Select(a => a.Action));
    }

    [Fact]
    public async Task Reactivating_requires_an_active_handling_department_again()
    {
        Categories.Categories[OldCategoryId].HandlingDepartmentId = InactiveDepartmentId;

        await Update(OldCategoryId, name: "שם חדש"); // still inactive: allowed
        var errors = await FieldErrors(() => Update(OldCategoryId, isActive: true));

        Assert.True(errors.ContainsKey("handlingDepartmentId"));
    }

    [Fact]
    public async Task An_unknown_category_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CategoryService.UpdateAsync(12345,
            new UpdateCategoryRequest("x", "y", RoutingType.RequesterDepartmentHr, null, true), Ip, CancellationToken.None));
    }

    // ===================== List and cache =====================

    [Fact]
    public async Task Inactive_categories_are_listed_for_admin_only()
    {
        var forAdmin = await CategoryService.GetAllAsync(includeInactive: true, CancellationToken.None);
        Assert.Contains(forAdmin, c => c.Id == OldCategoryId);
        Assert.Equal(GeneralId, forAdmin[^1].Id); // the fallback last

        CurrentUser.SignedInUserId = NoaId;
        CurrentUser.SignedInRoles = [Role.Employee];
        var forEmployee = await CategoryService.GetAllAsync(includeInactive: true, CancellationToken.None);
        Assert.DoesNotContain(forEmployee, c => c.Id == OldCategoryId);
    }

    [Fact]
    public async Task The_list_is_cached_and_a_change_evicts_it()
    {
        await CategoryService.GetAllAsync(includeInactive: false, CancellationToken.None);
        await CategoryService.GetAllAsync(includeInactive: true, CancellationToken.None);
        Assert.Equal(1, Categories.GetAllCount); // one entry per organization for both views

        await Update(PayrollId, isActive: false);
        var after = await CategoryService.GetAllAsync(includeInactive: false, CancellationToken.None);

        Assert.Equal(2, Categories.GetAllCount);
        Assert.DoesNotContain(after, c => c.Id == PayrollId);
    }

    [Fact]
    public async Task A_department_change_evicts_the_department_cache()
    {
        var before = await DepartmentService.GetAllAsync(includeInactive: false, CancellationToken.None);

        await DepartmentService.UpdateAsync(DevelopmentId,
            new UpdateDepartmentRequest("פיתוח תוכנה", RonId, true), Ip, CancellationToken.None);
        var after = await DepartmentService.GetAllAsync(includeInactive: false, CancellationToken.None);

        Assert.Equal("פיתוח", before.Single(d => d.Id == DevelopmentId).Name);
        Assert.Equal("פיתוח תוכנה", after.Single(d => d.Id == DevelopmentId).Name);
    }
}
