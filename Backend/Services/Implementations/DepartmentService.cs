using Common.Constants;
using Common.DTOs.Departments;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using ValidationException = Common.Exceptions.ValidationException;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;

namespace Services.Implementations;

/// <summary>
/// Departments (spec 7.8): unique name per organization, a manager chosen from the organization's active users,
/// deactivation only when no active user belongs to it. Changing the manager moves the DepartmentManager role
/// (<see cref="DepartmentManagerRules"/>). The list is cached per organization for 10 minutes (spec 11.3)
/// and evicted on every change.
/// </summary>
public sealed class DepartmentService : IDepartmentService
{
    public const string NotFoundTitle = "המחלקה לא נמצאה";
    public const string DuplicateNameMessage = "כבר קיימת מחלקה בשם זה";
    public const string InvalidManagerMessage = "יש לבחור עובד פעיל בארגון";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly IDepartmentRepository _departments;
    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IMemoryCache _cache;
    private readonly IValidator<CreateDepartmentRequest> _createValidator;
    private readonly IValidator<UpdateDepartmentRequest> _updateValidator;
    private readonly ILogger<DepartmentService> _logger;

    public DepartmentService(
        IDepartmentRepository departments,
        IUserRepository users,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IMemoryCache cache,
        IValidator<CreateDepartmentRequest> createValidator,
        IValidator<UpdateDepartmentRequest> updateValidator,
        ILogger<DepartmentService> logger)
    {
        _departments = departments;
        _users = users;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _cache = cache;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        // Inactive departments are an admin concern; everyone else always gets the active ones.
        includeInactive &= _currentUser.IsInRole(Role.Admin);

        var key = CacheKey(_currentUser.OrganizationId, includeInactive);
        if (_cache.TryGetValue(key, out IReadOnlyList<DepartmentDto>? cached) && cached is not null)
        {
            return cached;
        }

        var departments = await _departments.GetAllAsync(includeInactive, cancellationToken);
        _cache.Set(key, departments, CacheDuration);
        return departments;
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _createValidator.EnsureValidAsync(request, cancellationToken);

        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, excludeDepartmentId: null, cancellationToken);

        var department = new Department
        {
            OrganizationId = _currentUser.OrganizationId,
            Name = name,
            IsActive = true,
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            _departments.Add(department);
            await SaveTranslatingDuplicatesAsync(ct);
            // After the first save: the department needs an id before it can be a manager's department.
            await ChangeManagerAsync(department, request.ManagerUserId, ipAddress, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        EvictCache();
        _logger.LogInformation("Department {DepartmentId} created by admin {AdminUserId}", department.Id, _currentUser.UserId);
        return await GetDtoAsync(department.Id, cancellationToken);
    }

    public async Task<DepartmentDto> UpdateAsync(int departmentId, UpdateDepartmentRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _updateValidator.EnsureValidAsync(request, cancellationToken);

        var department = await _departments.FindForUpdateAsync(departmentId, cancellationToken)
                         ?? throw new NotFoundException(NotFoundTitle);

        var name = request.Name.Trim();
        if (name != department.Name)
        {
            await EnsureNameAvailableAsync(name, department.Id, cancellationToken);
        }

        var isActive = request.IsActive!.Value;
        if (department.IsActive && !isActive)
        {
            await EnsureCanDeactivateAsync(department, cancellationToken);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            department.Name = name;
            department.IsActive = isActive;
            await ChangeManagerAsync(department, request.ManagerUserId, ipAddress, ct);
            await SaveTranslatingDuplicatesAsync(ct);
            return true;
        }, cancellationToken);

        EvictCache();
        _logger.LogInformation("Department {DepartmentId} updated by admin {AdminUserId}", department.Id, _currentUser.UserId);
        return await GetDtoAsync(department.Id, cancellationToken);
    }

    // ===================== Helpers =====================

    /// <summary>
    /// Sets the manager and moves the DepartmentManager role: the new manager gets it (unless Executive covers it),
    /// the previous one loses it (decision in CLAUDE.md: a user manages at most one department, so they manage none now).
    /// </summary>
    private async Task ChangeManagerAsync(Department department, int? newManagerId, string ipAddress,
        CancellationToken cancellationToken)
    {
        if (newManagerId == department.ManagerUserId)
        {
            return;
        }

        User? newManager = null;
        if (newManagerId is { } id)
        {
            newManager = await _users.FindForUpdateAsync(id, cancellationToken);
            if (newManager is not { IsActive: true })
            {
                throw ValidationException.ForField("managerUserId", InvalidManagerMessage);
            }

            var other = await _departments.FindManagedByAsync(id, cancellationToken);
            DepartmentManagerRules.EnsureManagesNoOtherDepartment(other is not null && other.Id != department.Id ? other.Name : null);
        }

        var now = _clock.UtcNow;

        if (department.ManagerUserId is { } previousId
            && await _users.FindForUpdateAsync(previousId, cancellationToken) is { } previous)
        {
            var role = previous.Roles.FirstOrDefault(r => r.Role == Role.DepartmentManager);
            if (role is not null)
            {
                previous.Roles.Remove(role);
                Audit(AuditActions.UserRolesChanged, previous.Id, ipAddress, now);
            }
        }

        if (newManager is not null
            && DepartmentManagerRules.NeedsDepartmentManagerRole(newManager.Roles.Select(r => r.Role).ToList()))
        {
            newManager.Roles.Add(new UserRole { UserId = newManager.Id, Role = Role.DepartmentManager });
            Audit(AuditActions.UserRolesChanged, newManager.Id, ipAddress, now);
        }

        department.ManagerUserId = newManagerId;
    }

    private async Task EnsureCanDeactivateAsync(Department department, CancellationToken cancellationToken)
    {
        var activeUsers = await _departments.CountActiveUsersAsync(department.Id, cancellationToken);
        if (activeUsers > 0)
        {
            throw new BusinessRuleException(
                activeUsers == 1
                    ? "לא ניתן להשבית מחלקה שיש בה עובד פעיל"
                    : $"לא ניתן להשבית מחלקה שיש בה {activeUsers} עובדים פעילים",
                "יש להעביר את העובדים למחלקה אחרת או להשבית אותם קודם.", "department-has-active-users");
        }

        if (await _departments.IsHandlingDepartmentOfActiveCategoryAsync(department.Id, cancellationToken))
        {
            throw new BusinessRuleException("לא ניתן להשבית מחלקה שמטפלת בקטגוריה פעילה",
                "יש להעביר קודם את הקטגוריה למחלקה מטפלת אחרת.", "department-handles-category");
        }
    }

    private async Task EnsureNameAvailableAsync(string name, int? excludeDepartmentId, CancellationToken cancellationToken)
    {
        if (await _departments.NameExistsAsync(name, excludeDepartmentId, cancellationToken))
        {
            throw DuplicateName();
        }
    }

    private async Task SaveTranslatingDuplicatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.IndexName == UniqueIndexes.DepartmentName)
        {
            throw DuplicateName();
        }
    }

    private async Task<DepartmentDto> GetDtoAsync(int departmentId, CancellationToken cancellationToken) =>
        await _departments.GetDtoAsync(departmentId, cancellationToken) ?? throw new NotFoundException(NotFoundTitle);

    private void Audit(string action, int targetUserId, string ipAddress, DateTime now) =>
        _auditLogs.Add(AuditEntries.ForUser(action, _currentUser.OrganizationId, _currentUser.UserId, targetUserId,
            ipAddress, now));

    private void EvictCache()
    {
        _cache.Remove(CacheKey(_currentUser.OrganizationId, includeInactive: true));
        _cache.Remove(CacheKey(_currentUser.OrganizationId, includeInactive: false));
    }

    private static string CacheKey(int organizationId, bool includeInactive) =>
        $"departments:{organizationId}:{(includeInactive ? "all" : "active")}";

    private static ConflictException DuplicateName() =>
        new(DuplicateNameMessage, null, "duplicate-department-name",
            new Dictionary<string, string[]> { ["name"] = [DuplicateNameMessage] });
}
