using Common.Constants;
using Common.DTOs.Departments;
using Common.DTOs.Users;
using Common.Enums;
using Common.Exceptions;
using Repositories.Interfaces;
using Services.Implementations;

namespace Services.Tests.Admin;

public sealed class DepartmentServiceTests : AdminTestBase
{
    private Task<DepartmentDto> Update(int departmentId, string? name = null, int? managerUserId = -1, bool isActive = true)
    {
        var department = Departments.Departments[departmentId];
        return DepartmentService.UpdateAsync(departmentId,
            new UpdateDepartmentRequest(name ?? department.Name,
                managerUserId == -1 ? department.ManagerUserId : managerUserId, isActive),
            Ip, CancellationToken.None);
    }

    // ===================== Name =====================

    [Theory]
    [InlineData("פיתוח")]
    [InlineData("  פיתוח ")]
    public async Task A_name_taken_in_the_organization_is_a_conflict_on_the_name_field(string name)
    {
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            DepartmentService.CreateAsync(new CreateDepartmentRequest(name, null), Ip, CancellationToken.None));

        Assert.Equal([DepartmentService.DuplicateNameMessage], exception.Errors["name"]);
    }

    [Fact]
    public async Task Renaming_to_a_taken_name_is_a_conflict_and_keeping_the_own_name_is_not()
    {
        await Assert.ThrowsAsync<ConflictException>(() => Update(DevelopmentId, name: "הנהלה"));

        var result = await Update(DevelopmentId, name: "פיתוח");
        Assert.Equal("פיתוח", result.Name);
    }

    [Fact]
    public async Task A_concurrent_create_with_the_same_name_is_the_same_conflict()
    {
        UnitOfWork.ThrowOnNextSave = new UniqueConstraintException(UniqueIndexes.DepartmentName);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            DepartmentService.CreateAsync(new CreateDepartmentRequest("שיווק", null), Ip, CancellationToken.None));

        Assert.True(exception.Errors.ContainsKey("name"));
    }

    [Fact]
    public async Task Create_with_a_manager_gives_the_manager_the_DepartmentManager_role()
    {
        var result = await DepartmentService.CreateAsync(new CreateDepartmentRequest("שיווק", NoaId), Ip, CancellationToken.None);

        Assert.True(result.IsActive);
        Assert.Equal(NoaId, result.Manager?.Id);
        Assert.Contains(Role.DepartmentManager, RolesOf(UserById(NoaId)));
    }

    // ===================== Manager rule =====================

    [Fact]
    public async Task Replacing_the_manager_moves_the_DepartmentManager_role()
    {
        await Update(DevelopmentId, managerUserId: NoaId);

        Assert.Equal(NoaId, Departments.Departments[DevelopmentId].ManagerUserId);
        Assert.Contains(Role.DepartmentManager, RolesOf(UserById(NoaId)));
        Assert.DoesNotContain(Role.DepartmentManager, RolesOf(UserById(RonId)));
        Assert.All(AuditLog.Entries, a => Assert.Equal(AuditActions.UserRolesChanged, a.Action));
        Assert.Equal([RonId, NoaId], AuditLog.Entries.Select(a => a.EntityId!.Value));
    }

    [Fact]
    public async Task Removing_the_manager_removes_the_role()
    {
        await Update(DevelopmentId, managerUserId: null);

        Assert.Null(Departments.Departments[DevelopmentId].ManagerUserId);
        Assert.Equal([Role.Employee], RolesOf(UserById(RonId)));
    }

    [Fact]
    public async Task An_executive_manager_does_not_get_the_DepartmentManager_role()
    {
        AddUser(40, "deputy.ceo", ManagementId, Role.Executive);

        await Update(DevelopmentId, managerUserId: 40);

        Assert.Equal([Role.Employee, Role.Executive], RolesOf(UserById(40)));
    }

    [Fact]
    public async Task A_user_who_manages_another_department_cannot_be_appointed()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Update(DevelopmentId, managerUserId: YaelId));

        Assert.Equal("manages-other-department", exception.ErrorType);
        Assert.Equal(RonId, Departments.Departments[DevelopmentId].ManagerUserId);
    }

    [Theory]
    [InlineData(OritId)] // inactive
    [InlineData(12345)]  // not in the organization
    public async Task The_manager_must_be_an_active_user_of_the_organization(int managerId)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => Update(DevelopmentId, managerUserId: managerId));

        Assert.Equal([DepartmentService.InvalidManagerMessage], exception.Errors["managerUserId"]);
    }

    [Fact]
    public async Task Keeping_the_same_manager_changes_no_roles()
    {
        await Update(DevelopmentId, name: "פיתוח תוכנה");

        Assert.Empty(AuditLog.Entries);
        Assert.Contains(Role.DepartmentManager, RolesOf(UserById(RonId)));
    }

    // ===================== Deactivation =====================

    [Fact]
    public async Task A_department_with_active_users_cannot_be_deactivated()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Update(DevelopmentId, isActive: false));

        Assert.Equal("department-has-active-users", exception.ErrorType);
        Assert.Contains("3", exception.Title);
        Assert.True(Departments.Departments[DevelopmentId].IsActive);
    }

    [Fact]
    public async Task A_department_that_handles_an_active_category_cannot_be_deactivated()
    {
        var empty = await DepartmentService.CreateAsync(new CreateDepartmentRequest("שיווק", null), Ip, CancellationToken.None);
        Departments.HandlingDepartmentIds.Add(empty.Id);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Update(empty.Id, isActive: false));

        Assert.Equal("department-handles-category", exception.ErrorType);
    }

    [Fact]
    public async Task An_empty_department_can_be_deactivated_and_reactivated()
    {
        var empty = await DepartmentService.CreateAsync(new CreateDepartmentRequest("שיווק", null), Ip, CancellationToken.None);

        Assert.False((await Update(empty.Id, isActive: false)).IsActive);
        Assert.True((await Update(empty.Id, isActive: true)).IsActive);
    }

    // ===================== Read =====================

    [Fact]
    public async Task Inactive_departments_are_listed_for_admins_only()
    {
        var forAdmin = await DepartmentService.GetAllAsync(includeInactive: true, CancellationToken.None);
        Assert.Contains(forAdmin, d => d.Id == InactiveDepartmentId);

        CurrentUser.SignedInUserId = NoaId;
        CurrentUser.SignedInRoles = [Role.Employee];
        var forEmployee = await DepartmentService.GetAllAsync(includeInactive: true, CancellationToken.None);
        Assert.DoesNotContain(forEmployee, d => d.Id == InactiveDepartmentId);
    }

    [Fact]
    public async Task A_change_evicts_the_cached_list()
    {
        var before = await DepartmentService.GetAllAsync(includeInactive: false, CancellationToken.None);

        await DepartmentService.CreateAsync(new CreateDepartmentRequest("שיווק", null), Ip, CancellationToken.None);
        var after = await DepartmentService.GetAllAsync(includeInactive: false, CancellationToken.None);

        Assert.Equal(before.Count + 1, after.Count);
    }

    [Fact]
    public async Task A_change_to_an_employee_evicts_the_cached_department_counts()
    {
        var before = await DepartmentService.GetAllAsync(includeInactive: false, CancellationToken.None);
        Assert.Equal(3, before.Single(d => d.Id == DevelopmentId).ActiveUserCount);

        await UserService.SetActiveAsync(NoaId, new SetUserActiveRequest(false), Ip, CancellationToken.None);
        var after = await DepartmentService.GetAllAsync(includeInactive: false, CancellationToken.None);

        Assert.Equal(2, after.Single(d => d.Id == DevelopmentId).ActiveUserCount);
    }

    [Fact]
    public async Task An_unknown_department_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => DepartmentService.UpdateAsync(12345,
            new UpdateDepartmentRequest("x", null, true), Ip, CancellationToken.None));
    }
}
