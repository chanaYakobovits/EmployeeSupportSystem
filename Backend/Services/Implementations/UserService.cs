using Common.Constants;
using Common.DTOs.Paging;
using Common.DTOs.Users;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using ValidationException = Common.Exceptions.ValidationException;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Implementations.Invitations;
using Services.Interfaces;
using Services.Mapping;
using Services.Validators;

namespace Services.Implementations;

/// <summary>
/// User administration (spec 4.2, 7.8). All rules live here; the controller only passes requests through.
/// Protection rules (all → 422): an admin cannot deactivate themself or remove their own Admin role; the organization
/// always keeps an active Admin (checked under a lock); the DepartmentManager role follows the departments
/// (<see cref="DepartmentManagerRules"/>); a department manager cannot be deactivated.
/// A new user gets no password but an invitation e-mail (queued in the same transaction); a password reset gives a
/// temporary password and cancels open invitations. Temporary passwords exist only in the response: never logged,
/// audited or stored except as a hash.
/// </summary>
public sealed class UserService : IUserService
{
    public const string UserNotFoundTitle = "העובד לא נמצא";
    public const string DuplicateEmailMessage = "כתובת המייל כבר רשומה במערכת";
    public const string InactiveDepartmentMessage = "המחלקה שנבחרה לא קיימת או אינה פעילה";

    private readonly IUserRepository _users;
    private readonly IDepartmentRepository _departments;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITicketRepository _tickets;
    private readonly IUserInvitationRepository _invitations;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IReferenceDataCache _referenceData;
    private readonly IValidator<UserListQuery> _listValidator;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;
    private readonly IValidator<SetUserActiveRequest> _setActiveValidator;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository users,
        IDepartmentRepository departments,
        IRefreshTokenRepository refreshTokens,
        ITicketRepository tickets,
        IUserInvitationRepository invitations,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IReferenceDataCache referenceData,
        IValidator<UserListQuery> listValidator,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        IValidator<SetUserActiveRequest> setActiveValidator,
        ILogger<UserService> logger)
    {
        _users = users;
        _departments = departments;
        _refreshTokens = refreshTokens;
        _tickets = tickets;
        _invitations = invitations;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _currentUser = currentUser;
        _referenceData = referenceData;
        _listValidator = listValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _setActiveValidator = setActiveValidator;
        _logger = logger;
    }

    // ===================== Read =====================

    public async Task<PagedResult<UserListItemDto>> GetPageAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        await _listValidator.EnsureValidAsync(query, cancellationToken);
        return await _users.GetPageAsync(query, _clock.UtcNow, cancellationToken);
    }

    public async Task<UserDetailsDto> GetAsync(int userId, CancellationToken cancellationToken) =>
        await _users.GetDetailsAsync(userId, _clock.UtcNow, cancellationToken) ?? throw UserNotFound();

    // ===================== Create =====================

    public async Task<UserDetailsDto> CreateAsync(CreateUserRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _createValidator.EnsureValidAsync(request, cancellationToken);

        await EnsureActiveDepartmentAsync(request.DepartmentId, cancellationToken);
        var roles = UserMapping.NormalizeRoles(request.Roles);
        // A new user manages no department yet.
        DepartmentManagerRules.EnsureConsistent(roles, managedDepartmentName: null);

        var email = UserMapping.NormalizeEmail(request.Email);
        if (await _users.EmailExistsAsync(email, excludeUserId: null, cancellationToken))
        {
            throw DuplicateEmail();
        }

        var now = _clock.UtcNow;

        var user = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var employeeCode = EmployeeCodeFormat.Format(await _users.NextEmployeeNumberAsync(ct));
            // No password: the employee sets one through the invitation e-mail. The job is saved with the user,
            // so a user never exists without their invitation (the worker sends it moments later).
            var newUser = request.ToNewUser(_currentUser.OrganizationId, employeeCode, roles, now);
            _users.Add(newUser);
            _invitations.AddJob(InvitationQueue.NewJob(newUser, now));
            await SaveTranslatingDuplicatesAsync(ct);

            // Second save in the same transaction: the audit record needs the new user's id.
            Audit(AuditActions.UserCreated, newUser.Id, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(ct);
            return newUser;
        }, cancellationToken);

        InvalidateDepartments();
        _logger.LogInformation("User {UserId} created by admin {AdminUserId}; invitation queued", user.Id, _currentUser.UserId);
        return await GetAsync(user.Id, cancellationToken);
    }

    // ===================== Invitation =====================

    public async Task<UserDetailsDto> ResendInvitationAsync(int userId, string ipAddress, CancellationToken cancellationToken)
    {
        var user = await _users.FindForUpdateAsync(userId, cancellationToken) ?? throw UserNotFound();

        if (!user.IsActive)
        {
            throw new BusinessRuleException("העובד אינו פעיל",
                "יש להפעיל את העובד לפני שליחת הזמנה.", "inactive-user");
        }

        if (!user.MustChangePassword)
        {
            throw new BusinessRuleException("העובד כבר קבע סיסמה",
                "אפשר לאפס לו סיסמה במקום לשלוח הזמנה.", "password-already-set");
        }

        var now = _clock.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Earlier links stop working and a queued e-mail is not sent twice.
            await InvitationQueue.CancelOpenAsync(_invitations, user.Id, now, ct);
            _invitations.AddJob(InvitationQueue.NewJob(user, now));
            Audit(AuditActions.InvitationResent, user.Id, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        _logger.LogInformation("Invitation of user {UserId} resent by admin {AdminUserId}", user.Id, _currentUser.UserId);
        return await GetAsync(user.Id, cancellationToken);
    }

    // ===================== Update =====================

    public async Task<UserDetailsDto> UpdateAsync(int userId, UpdateUserRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _updateValidator.EnsureValidAsync(request, cancellationToken);

        var user = await _users.FindForUpdateAsync(userId, cancellationToken) ?? throw UserNotFound();

        if (request.DepartmentId != user.DepartmentId)
        {
            await EnsureActiveDepartmentAsync(request.DepartmentId, cancellationToken);
        }

        var roles = UserMapping.NormalizeRoles(request.Roles);
        var managed = await _departments.FindManagedByAsync(user.Id, cancellationToken);
        DepartmentManagerRules.EnsureConsistent(roles, managed?.Name);

        var removesAdmin = user.Roles.Any(r => r.Role == Role.Admin) && !roles.Contains(Role.Admin);
        if (removesAdmin && user.Id == _currentUser.UserId)
        {
            throw new BusinessRuleException("לא ניתן להסיר את הרשאת מנהל המערכת מהמשתמש שלך",
                "מנהל מערכת אחר יכול לעשות זאת.", "self-admin-removal");
        }

        var email = UserMapping.NormalizeEmail(request.Email);
        if (email != user.Email && await _users.EmailExistsAsync(email, user.Id, cancellationToken))
        {
            throw DuplicateEmail();
        }

        var now = _clock.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (removesAdmin)
            {
                await EnsureAnotherActiveAdminAsync(user.Id, ct);
            }

            if (request.ApplyDetails(user))
            {
                Audit(AuditActions.UserUpdated, user.Id, ipAddress, now);
            }

            if (user.SetRoles(roles))
            {
                Audit(AuditActions.UserRolesChanged, user.Id, ipAddress, now);
            }

            await SaveTranslatingDuplicatesAsync(ct);
            return true;
        }, cancellationToken);

        InvalidateDepartments();
        _logger.LogInformation("User {UserId} updated by admin {AdminUserId}", user.Id, _currentUser.UserId);
        return await GetAsync(user.Id, cancellationToken);
    }

    // ===================== Reset password =====================

    public async Task<ResetPasswordResponse> ResetPasswordAsync(int userId, string ipAddress,
        CancellationToken cancellationToken)
    {
        if (userId == _currentUser.UserId)
        {
            throw new BusinessRuleException("לא ניתן לאפס את הסיסמה של המשתמש שלך",
                "יש להשתמש בהחלפת סיסמה.", "self-password-reset");
        }

        var user = await _users.FindForUpdateAsync(userId, cancellationToken) ?? throw UserNotFound();

        var now = _clock.UtcNow;
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        user.PasswordHash = _passwordHasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        var revoked = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // The temporary password replaces any invitation: open links stop working, queued e-mails are not sent.
            await InvitationQueue.CancelOpenAsync(_invitations, user.Id, now, ct);
            var count = await RevokeAllRefreshTokensAsync(user.Id, now, ct);
            Audit(AuditActions.UserPasswordReset, user.Id, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(ct);
            return count;
        }, cancellationToken);

        _logger.LogInformation("Password of user {UserId} reset by admin {AdminUserId}; revoked {Count} refresh tokens",
            user.Id, _currentUser.UserId, revoked);
        return new ResetPasswordResponse(temporaryPassword);
    }

    // ===================== Activate / deactivate =====================

    public async Task<SetUserActiveResponse> SetActiveAsync(int userId, SetUserActiveRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _setActiveValidator.EnsureValidAsync(request, cancellationToken);
        var isActive = request.IsActive!.Value;

        var user = await _users.FindForUpdateAsync(userId, cancellationToken) ?? throw UserNotFound();

        if (user.IsActive == isActive)
        {
            // Already in the requested state: nothing to do, nothing to audit.
            return new SetUserActiveResponse(await GetAsync(user.Id, cancellationToken), 0);
        }

        var unassigned = isActive
            ? await ActivateAsync(user, ipAddress, cancellationToken)
            : await DeactivateAsync(user, ipAddress, cancellationToken);

        return new SetUserActiveResponse(await GetAsync(user.Id, cancellationToken), unassigned);
    }

    private async Task<int> ActivateAsync(User user, string ipAddress, CancellationToken cancellationToken)
    {
        // A department with active users cannot be deactivated, so its users cannot be reactivated into it either.
        if (!user.Department.IsActive)
        {
            throw new BusinessRuleException($"מחלקת {user.Department.Name} אינה פעילה",
                "יש לשייך את העובד למחלקה פעילה לפני ההפעלה.", "inactive-department");
        }

        var now = _clock.UtcNow;
        user.IsActive = true;
        Audit(AuditActions.UserActivated, user.Id, ipAddress, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        InvalidateDepartments();
        _logger.LogInformation("User {UserId} activated by admin {AdminUserId}", user.Id, _currentUser.UserId);
        return 0;
    }

    private async Task<int> DeactivateAsync(User user, string ipAddress, CancellationToken cancellationToken)
    {
        if (user.Id == _currentUser.UserId)
        {
            throw new BusinessRuleException("לא ניתן להשבית את המשתמש שלך",
                "מנהל מערכת אחר יכול לעשות זאת.", "self-deactivation");
        }

        // Decision in CLAUDE.md: blocked, so a department never loses its manager without someone choosing it.
        var managed = await _departments.FindManagedByAsync(user.Id, cancellationToken);
        if (managed is not null)
        {
            throw new BusinessRuleException($"העובד מנהל את מחלקת {managed.Name}",
                "יש להחליף קודם את מנהל המחלקה במסך המחלקות, ואז להשבית.", "department-manager-deactivation");
        }

        var now = _clock.UtcNow;
        var unassigned = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (user.Roles.Any(r => r.Role == Role.Admin))
            {
                await EnsureAnotherActiveAdminAsync(user.Id, ct);
            }

            user.IsActive = false;
            await RevokeAllRefreshTokensAsync(user.Id, now, ct);
            // Decision in CLAUDE.md: no ticket stays with a handler who can no longer sign in.
            var count = await _tickets.UnassignUnresolvedTicketsAsync(user.Id, now, ct);
            Audit(AuditActions.UserDeactivated, user.Id, ipAddress, now);
            await _unitOfWork.SaveChangesAsync(ct);
            return count;
        }, cancellationToken);

        InvalidateDepartments();
        _logger.LogInformation("User {UserId} deactivated by admin {AdminUserId}; {Count} tickets returned to the queue",
            user.Id, _currentUser.UserId, unassigned);
        return unassigned;
    }

    // ===================== Helpers =====================

    /// <summary>
    /// The organization must keep at least one active Admin besides <paramref name="userId"/>.
    /// The lock serializes concurrent requests (two admins deactivating each other), so the count is reliable.
    /// </summary>
    private async Task EnsureAnotherActiveAdminAsync(int userId, CancellationToken cancellationToken)
    {
        await _users.LockOrganizationAdminsAsync(_currentUser.OrganizationId, cancellationToken);
        if (await _users.CountActiveAdminsAsync(userId, cancellationToken) == 0)
        {
            throw new BusinessRuleException("לא ניתן להשאיר את הארגון בלי מנהל מערכת פעיל",
                "יש להגדיר קודם מנהל מערכת נוסף.", "last-admin");
        }
    }

    private async Task EnsureActiveDepartmentAsync(int departmentId, CancellationToken cancellationToken)
    {
        if (await _departments.GetActiveNameAsync(departmentId, cancellationToken) is null)
        {
            throw ValidationException.ForField("departmentId", InactiveDepartmentMessage);
        }
    }

    private async Task<int> RevokeAllRefreshTokensAsync(int userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await _refreshTokens.GetActiveForUserAsync(userId, now, cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAt = now;
        }

        return active.Count;
    }

    /// <summary>Two admins creating the same e-mail at once both pass the pre-check; the unique index decides.</summary>
    private async Task SaveTranslatingDuplicatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.IndexName == UniqueIndexes.UserEmail)
        {
            throw DuplicateEmail();
        }
    }

    private void Audit(string action, int targetUserId, string ipAddress, DateTime now) =>
        _auditLogs.Add(AuditEntries.ForUser(action, _currentUser.OrganizationId, _currentUser.UserId, targetUserId,
            ipAddress, now));

    private static NotFoundException UserNotFound() => new(UserNotFoundTitle);

    private static ConflictException DuplicateEmail() =>
        new(DuplicateEmailMessage, "כל עובד צריך כתובת מייל משלו.", "duplicate-email",
            new Dictionary<string, string[]> { ["email"] = [DuplicateEmailMessage] });

    /// <summary>
    /// The cached department list shows active employee and handler counts and the manager's name (spec 11.3),
    /// so a change to a user makes it stale.
    /// </summary>
    private void InvalidateDepartments() => _referenceData.Invalidate(ReferenceData.Departments, _currentUser.OrganizationId);
}
