using Common.Constants;
using Common.DTOs.Users;
using Common.Enums;
using Common.Exceptions;
using Repositories.Interfaces;
using Services.Implementations;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Admin;

public sealed class UserServiceTests : AdminTestBase
{
    private static CreateUserRequest NewUser(string email = "dana.new@example.com", int departmentId = DevelopmentId,
        IReadOnlyList<Role>? roles = null) =>
        new("דנה", "חדשה", email, departmentId, "מפתחת", roles);

    private UpdateUserRequest UpdateOf(int userId, IReadOnlyList<Role>? roles = null, string? email = null,
        int? departmentId = null)
    {
        var user = UserById(userId);
        return new UpdateUserRequest(user.FirstName, user.LastName, email ?? user.Email,
            departmentId ?? user.DepartmentId, user.JobTitle, roles ?? RolesOf(user));
    }

    private Task<UserDetailsDto> Update(int userId, UpdateUserRequest request) =>
        UserService.UpdateAsync(userId, request, Ip, CancellationToken.None);

    private Task<SetUserActiveResponse> SetActive(int userId, bool isActive) =>
        UserService.SetActiveAsync(userId, new SetUserActiveRequest(isActive), Ip, CancellationToken.None);

    // ===================== Create =====================

    [Fact]
    public async Task Create_gives_the_next_employee_code_from_the_sequence()
    {
        Users.NextEmployeeNumber = 42;

        var first = await UserService.CreateAsync(NewUser("a@example.com"), Ip, CancellationToken.None);
        var second = await UserService.CreateAsync(NewUser("b@example.com"), Ip, CancellationToken.None);

        Assert.Equal("EMP-00042", first.EmployeeCode);
        Assert.Equal("EMP-00043", second.EmployeeCode);
    }

    [Theory]
    [InlineData(new Role[0])]
    [InlineData(new[] { Role.Handler })]
    [InlineData(new[] { Role.Handler, Role.Handler, Role.Employee })]
    public async Task Create_always_includes_Employee_without_duplicates(Role[] requested)
    {
        var response = await UserService.CreateAsync(NewUser(roles: requested), Ip, CancellationToken.None);

        var expected = requested.Append(Role.Employee).Distinct().Order().ToList();
        Assert.Equal(expected, response.Roles);
        Assert.Equal(expected, RolesOf(UserById(response.Id)));
    }

    [Fact]
    public async Task Create_with_null_roles_gives_Employee_only()
    {
        var response = await UserService.CreateAsync(NewUser(roles: null), Ip, CancellationToken.None);

        Assert.Equal([Role.Employee], response.Roles);
    }

    [Fact]
    public async Task Create_makes_a_user_without_a_password_who_must_set_one()
    {
        var response = await UserService.CreateAsync(NewUser(), Ip, CancellationToken.None);
        var user = UserById(response.Id);

        Assert.Null(user.PasswordHash);
        Assert.True(user.MustChangePassword);
        Assert.True(user.IsActive);
        Assert.Equal(OrgId, user.OrganizationId);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public async Task Create_queues_an_invitation_in_the_same_transaction_without_a_token()
    {
        var response = await UserService.CreateAsync(NewUser(), Ip, CancellationToken.None);

        var job = Assert.Single(Invitations.Jobs);
        Assert.Equal(response.Id, job.UserId);
        Assert.Equal(OrgId, job.OrganizationId);
        Assert.Equal(InvitationJobStatus.Pending, job.Status);
        Assert.Equal(Now, job.NextAttemptAt);
        Assert.Null(job.InvitationId);
        Assert.Empty(Invitations.Invitations); // the token is created only when the e-mail is sent
        Assert.Equal(1, UnitOfWork.TransactionCount);
        Assert.Equal(InvitationStatus.Pending, response.Invitation?.Status);
    }

    [Fact]
    public async Task Create_is_audited_with_actor_target_and_ip()
    {
        var response = await UserService.CreateAsync(NewUser(), Ip, CancellationToken.None);

        var audit = Assert.Single(AuditLog.Entries);
        Assert.Equal(AuditActions.UserCreated, audit.Action);
        Assert.Equal(YaelId, audit.UserId);
        Assert.Equal(response.Id, audit.EntityId);
        Assert.Equal(Ip, audit.IpAddress);
    }

    [Fact]
    public async Task Create_normalizes_the_email()
    {
        var response = await UserService.CreateAsync(NewUser("  Dana.New@Example.COM "), Ip, CancellationToken.None);

        Assert.Equal("dana.new@example.com", response.Email);
    }

    [Theory]
    [InlineData("noa.berkovich@example.com")]
    [InlineData(" NOA.Berkovich@Example.com ")]
    public async Task Create_with_an_existing_email_is_a_conflict_on_the_email_field(string email)
    {
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            UserService.CreateAsync(NewUser(email), Ip, CancellationToken.None));

        Assert.Equal(UserService.DuplicateEmailMessage, exception.Title);
        Assert.Equal([UserService.DuplicateEmailMessage], exception.Errors["email"]);
        Assert.Equal(6, Users.Users.Count);
    }

    [Fact]
    public async Task Create_racing_another_request_for_the_same_email_is_the_same_conflict()
    {
        // The pre-check passed, but another admin's insert won: the unique index rejects ours.
        UnitOfWork.ThrowOnNextSave = new UniqueConstraintException(UniqueIndexes.UserEmail);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            UserService.CreateAsync(NewUser(), Ip, CancellationToken.None));

        Assert.Equal("duplicate-email", exception.ErrorType);
        Assert.True(exception.Errors.ContainsKey("email"));
    }

    [Fact]
    public async Task Create_with_the_DepartmentManager_role_is_rejected()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            UserService.CreateAsync(NewUser(roles: [Role.DepartmentManager]), Ip, CancellationToken.None));

        Assert.Equal(DepartmentManagerRules.RoleOnlyThroughDepartmentsMessage, exception.Title);
    }

    [Theory]
    [InlineData(InactiveDepartmentId)]
    [InlineData(999)]
    public async Task Create_in_an_inactive_or_unknown_department_is_a_field_error(int departmentId)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            UserService.CreateAsync(NewUser(departmentId: departmentId), Ip, CancellationToken.None));

        Assert.Equal([UserService.InactiveDepartmentMessage], exception.Errors["departmentId"]);
    }

    [Fact]
    public async Task Create_with_invalid_fields_reports_each_field_in_camelCase()
    {
        var request = new CreateUserRequest(" ", new string('א', 51), "not-an-email", 0, "", null);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            UserService.CreateAsync(request, Ip, CancellationToken.None));

        Assert.Equal(["departmentId", "email", "firstName", "jobTitle", "lastName"],
            exception.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["יש להזין שם פרטי"], exception.Errors["firstName"]);
        Assert.Equal(["כתובת המייל אינה תקינה"], exception.Errors["email"]);
    }

    // ===================== Update =====================

    [Fact]
    public async Task Update_changes_details_but_never_the_employee_code()
    {
        var request = new UpdateUserRequest("נועה", "ברקוביץ-לוי", "noa.levi@example.com", HrId, "ראש צוות", [Role.Handler]);

        var result = await Update(NoaId, request);

        Assert.Equal("EMP-00004", result.EmployeeCode);
        Assert.Equal("ברקוביץ-לוי", result.LastName);
        Assert.Equal("noa.levi@example.com", result.Email);
        Assert.Equal(HrId, UserById(NoaId).DepartmentId);
        Assert.Equal([Role.Employee, Role.Handler], RolesOf(UserById(NoaId)));
        Assert.Equal([AuditActions.UserUpdated, AuditActions.UserRolesChanged], AuditLog.Entries.Select(a => a.Action));
    }

    [Fact]
    public async Task Update_without_changes_writes_no_audit()
    {
        await Update(NoaId, UpdateOf(NoaId));

        Assert.Empty(AuditLog.Entries);
    }

    [Fact]
    public async Task Update_to_an_email_of_another_user_is_a_conflict()
    {
        await Assert.ThrowsAsync<ConflictException>(() => Update(NoaId, UpdateOf(NoaId, email: "liat.shahar@example.com")));
    }

    [Fact]
    public async Task Update_of_a_user_that_does_not_exist_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Update(12345, new UpdateUserRequest("a", "b", "x@example.com", DevelopmentId, "j", null)));
    }

    [Fact]
    public async Task Admin_cannot_remove_the_Admin_role_from_themself()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Update(YaelId, UpdateOf(YaelId, roles: [Role.Handler, Role.DepartmentManager])));

        Assert.Equal("self-admin-removal", exception.ErrorType);
        Assert.Contains(Role.Admin, RolesOf(UserById(YaelId)));
    }

    [Fact]
    public async Task The_Admin_role_cannot_be_removed_from_the_last_active_admin()
    {
        SignInAsDemotedAdmin();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Update(YaelId, UpdateOf(YaelId, roles: [Role.Handler, Role.DepartmentManager])));

        Assert.Equal("last-admin", exception.ErrorType);
        Assert.Equal(1, Users.LockCount);
    }

    [Fact]
    public async Task The_Admin_role_can_be_removed_when_another_active_admin_remains()
    {
        AddUser(30, "second.admin", HrId, Role.Admin);
        CurrentUser.SignedInUserId = 30;

        await Update(YaelId, UpdateOf(YaelId, roles: [Role.Handler, Role.DepartmentManager]));

        Assert.DoesNotContain(Role.Admin, RolesOf(UserById(YaelId)));
        Assert.Equal(1, Users.LockCount);
    }

    [Fact]
    public async Task An_inactive_second_admin_does_not_count()
    {
        AddUser(30, "second.admin", HrId, Role.Admin).IsActive = false;
        SignInAsDemotedAdmin();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Update(YaelId, UpdateOf(YaelId, roles: [Role.DepartmentManager])));
    }

    [Fact]
    public async Task The_DepartmentManager_role_cannot_be_removed_from_a_department_manager()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => Update(RonId, UpdateOf(RonId, roles: [])));

        Assert.Equal("department-manager-role", exception.ErrorType);
        Assert.Contains("פיתוח", exception.Title);
    }

    [Fact]
    public async Task The_DepartmentManager_role_cannot_be_added_on_the_users_screen()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Update(NoaId, UpdateOf(NoaId, roles: [Role.DepartmentManager])));
    }

    [Fact]
    public async Task An_executive_manages_a_department_without_the_DepartmentManager_role()
    {
        await Update(AvnerId, UpdateOf(AvnerId, roles: [Role.Executive]));

        // Executive removed: now the manager of Management must hold DepartmentManager.
        await Assert.ThrowsAsync<BusinessRuleException>(() => Update(AvnerId, UpdateOf(AvnerId, roles: [])));
    }

    // ===================== Reset password =====================

    [Fact]
    public async Task Reset_password_sets_a_new_temporary_password_clears_the_lockout_and_revokes_all_sessions()
    {
        var noa = UserById(NoaId);
        noa.FailedLoginCount = 3;
        noa.LockoutEnd = Now.AddMinutes(10);
        AddActiveRefreshTokens(NoaId, 2);

        var response = await UserService.ResetPasswordAsync(NoaId, Ip, CancellationToken.None);

        Assert.Empty(PasswordPolicy.Validate(response.TemporaryPassword));
        Assert.True(Hasher.Verify(noa.PasswordHash!, response.TemporaryPassword));
        Assert.True(noa.MustChangePassword);
        Assert.Equal(0, noa.FailedLoginCount);
        Assert.Null(noa.LockoutEnd);
        Assert.Empty(RefreshTokens.ActiveFor(NoaId, Now));
        Assert.Equal(AuditActions.UserPasswordReset, Assert.Single(AuditLog.Entries).Action);
        Assert.DoesNotContain(response.TemporaryPassword, UserLogger.AllText);
    }

    [Fact]
    public async Task Admin_cannot_reset_their_own_password()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => UserService.ResetPasswordAsync(YaelId, Ip, CancellationToken.None));
    }

    // ===================== Activate / deactivate =====================

    [Fact]
    public async Task Admin_cannot_deactivate_themself()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => SetActive(YaelId, false));

        Assert.Equal("self-deactivation", exception.ErrorType);
        Assert.True(UserById(YaelId).IsActive);
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_deactivated()
    {
        // Yael would otherwise be blocked as a department manager first; the admin check is what is tested here.
        Departments.Departments[HrId].ManagerUserId = null;
        UserById(YaelId).Roles.Remove(UserById(YaelId).Roles.Single(r => r.Role == Role.DepartmentManager));
        SignInAsDemotedAdmin();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => SetActive(YaelId, false));

        Assert.Equal("last-admin", exception.ErrorType);
        Assert.True(UserById(YaelId).IsActive);
    }

    [Fact]
    public async Task A_department_manager_cannot_be_deactivated()
    {
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => SetActive(RonId, false));

        Assert.Equal("department-manager-deactivation", exception.ErrorType);
        Assert.Contains("פיתוח", exception.Title);
    }

    [Fact]
    public async Task Deactivating_a_handler_revokes_all_sessions_and_returns_unresolved_tickets_to_the_queue()
    {
        AddActiveRefreshTokens(LiatId, 2);
        Tickets.UnresolvedByHandler[LiatId] = 3;

        var response = await SetActive(LiatId, false);

        Assert.False(response.User.IsActive);
        Assert.False(UserById(LiatId).IsActive);
        Assert.Equal(3, response.UnassignedTicketCount);
        Assert.Equal([LiatId], Tickets.UnassignedHandlers);
        Assert.Empty(RefreshTokens.ActiveFor(LiatId, Now));
        Assert.Equal(AuditActions.UserDeactivated, Assert.Single(AuditLog.Entries).Action);
        Assert.Equal(1, UnitOfWork.TransactionCount);
    }

    [Fact]
    public async Task Setting_the_current_state_again_changes_nothing()
    {
        var response = await SetActive(NoaId, true);

        Assert.True(response.User.IsActive);
        Assert.Empty(AuditLog.Entries);
        Assert.Equal(0, UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Activation_is_audited()
    {
        UserById(NoaId).IsActive = false;

        await SetActive(NoaId, true);

        Assert.True(UserById(NoaId).IsActive);
        Assert.Equal(AuditActions.UserActivated, Assert.Single(AuditLog.Entries).Action);
    }

    [Fact]
    public async Task A_user_of_an_inactive_department_cannot_be_activated()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => SetActive(OritId, true));

        Assert.False(UserById(OritId).IsActive);
    }

    [Fact]
    public async Task A_missing_isActive_value_is_a_field_error()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            UserService.SetActiveAsync(NoaId, new SetUserActiveRequest(null), Ip, CancellationToken.None));

        Assert.True(exception.Errors.ContainsKey("isActive"));
    }

    // ===================== Read =====================

    [Fact]
    public async Task Get_of_an_unknown_user_is_not_found()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => UserService.GetAsync(12345, CancellationToken.None));

        Assert.Equal(UserService.UserNotFoundTitle, exception.Title);
    }

    [Fact]
    public async Task Get_shows_the_managed_department()
    {
        var ron = await UserService.GetAsync(RonId, CancellationToken.None);
        var noa = await UserService.GetAsync(NoaId, CancellationToken.None);

        Assert.Equal(DevelopmentId, ron.ManagedDepartment?.Id);
        Assert.Null(noa.ManagedDepartment);
    }

    [Fact]
    public async Task An_invalid_page_size_is_rejected_before_any_query()
    {
        // FakeUserRepository.GetPageAsync throws: reaching it would fail the test with a different exception.
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            UserService.GetPageAsync(new UserListQuery { PageSize = 500 }, CancellationToken.None));

        Assert.True(exception.Errors.ContainsKey("pageSize"));
    }
}
