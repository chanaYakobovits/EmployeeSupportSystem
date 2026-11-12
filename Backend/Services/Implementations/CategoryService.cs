using Common.Constants;
using Common.DTOs.Categories;
using Common.Enums;
using Common.Exceptions;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;
using ValidationException = Common.Exceptions.ValidationException;

namespace Services.Implementations;

/// <summary>
/// Categories and routing (spec 3.5, 7.8). The code is unique in the organization and fixed at creation.
/// FixedDepartment needs an active handling department; RequesterDepartmentHr has none. The fallback category
/// (GENERAL) is always active and the flag can be neither moved nor removed – no request carries it.
/// Deactivating keeps the category on existing tickets; it is only no longer offered to the AI or for manual
/// classification. The list is cached per organization (spec 11.3) and evicted on every change.
/// </summary>
public sealed class CategoryService : ICategoryService
{
    public const string NotFoundTitle = "הקטגוריה לא נמצאה";
    public const string DuplicateCodeMessage = "כבר קיימת קטגוריה עם קוד זה";
    public const string InactiveDepartmentMessage = "יש לבחור מחלקה פעילה";
    public const string FallbackDeactivationTitle = "לא ניתן להשבית את קטגוריית הגיבוי";

    private readonly ICategoryRepository _categories;
    private readonly IDepartmentRepository _departments;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IReferenceDataCache _cache;
    private readonly IValidator<CreateCategoryRequest> _createValidator;
    private readonly IValidator<UpdateCategoryRequest> _updateValidator;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        ICategoryRepository categories,
        IDepartmentRepository departments,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IReferenceDataCache cache,
        IValidator<CreateCategoryRequest> createValidator,
        IValidator<UpdateCategoryRequest> updateValidator,
        ILogger<CategoryService> logger)
    {
        _categories = categories;
        _departments = departments;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _cache = cache;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public static string NoActiveHandlersWarning(string departmentName) =>
        $"אין במחלקה {departmentName} גורמים מטפלים פעילים. פניות בקטגוריה זו יועברו לתור משאבי אנוש מרכזי עד שיוגדר מטפל.";

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        // Inactive categories are an admin concern; everyone else always gets the active ones.
        includeInactive &= _currentUser.IsInRole(Role.Admin);

        // One cache entry per organization (spec 11.3) holding every category; a handful of rows.
        var all = await _cache.GetOrCreateAsync(ReferenceData.Categories, _currentUser.OrganizationId,
            _categories.GetAllAsync, cancellationToken);
        return includeInactive ? all : all.Where(c => c.IsActive).ToList();
    }

    public async Task<CategorySaveResult> CreateAsync(CreateCategoryRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _createValidator.EnsureValidAsync(request, cancellationToken);

        var code = request.Code.Trim();
        if (await _categories.CodeExistsAsync(code, cancellationToken))
        {
            throw DuplicateCode();
        }

        var routingType = request.RoutingType!.Value;
        var warnings = await CheckHandlingDepartmentAsync(routingType, request.HandlingDepartmentId, cancellationToken);

        var category = new Category
        {
            OrganizationId = _currentUser.OrganizationId,
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            RoutingType = routingType,
            HandlingDepartmentId = HandlingDepartmentOf(routingType, request.HandlingDepartmentId),
            IsFallback = false,
            IsActive = true,
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            _categories.Add(category);
            await SaveTranslatingDuplicatesAsync(ct);
            // Second save in the same transaction: the audit record needs the new category's id.
            Audit(AuditActions.CategoryCreated, category.Id, ipAddress);
            await _unitOfWork.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        EvictCache();
        _logger.LogInformation("Category {CategoryId} created by admin {AdminUserId}", category.Id, _currentUser.UserId);
        return new CategorySaveResult(await GetDtoAsync(category.Id, cancellationToken), warnings);
    }

    public async Task<CategorySaveResult> UpdateAsync(int categoryId, UpdateCategoryRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _updateValidator.EnsureValidAsync(request, cancellationToken);

        var category = await _categories.FindForUpdateAsync(categoryId, cancellationToken)
                       ?? throw new NotFoundException(NotFoundTitle);

        var isActive = request.IsActive!.Value;
        if (category.IsFallback && !isActive)
        {
            throw new BusinessRuleException(FallbackDeactivationTitle,
                "פניות שלא סווגו, או שה-AI לא הצליח לנתח, מגיעות אליה.", "fallback-category-deactivation");
        }

        var routingType = request.RoutingType!.Value;
        // An inactive category may keep pointing at a department closed since; it is checked again on reactivation.
        IReadOnlyList<string> warnings = isActive
            ? await CheckHandlingDepartmentAsync(routingType, request.HandlingDepartmentId, cancellationToken)
            : [];

        var wasActive = category.IsActive;
        category.Name = request.Name.Trim();
        category.Description = request.Description.Trim();
        category.RoutingType = routingType;
        category.HandlingDepartmentId = HandlingDepartmentOf(routingType, request.HandlingDepartmentId);
        category.IsActive = isActive;

        Audit(AuditActions.CategoryUpdated, category.Id, ipAddress);
        if (wasActive != isActive)
        {
            Audit(isActive ? AuditActions.CategoryActivated : AuditActions.CategoryDeactivated, category.Id, ipAddress);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        EvictCache();
        _logger.LogInformation("Category {CategoryId} updated by admin {AdminUserId}", category.Id, _currentUser.UserId);
        return new CategorySaveResult(await GetDtoAsync(category.Id, cancellationToken), warnings);
    }

    // ===================== Helpers =====================

    /// <summary>
    /// FixedDepartment: the department must exist and be active (→ 400 on handlingDepartmentId). A department without
    /// active handlers is allowed, with a warning: its tickets go to central HR until a handler is assigned (spec 3.5).
    /// </summary>
    private async Task<IReadOnlyList<string>> CheckHandlingDepartmentAsync(RoutingType routingType, int? departmentId,
        CancellationToken cancellationToken)
    {
        if (routingType != RoutingType.FixedDepartment)
        {
            return [];
        }

        var department = await _departments.GetDtoAsync(departmentId!.Value, cancellationToken);
        if (department is not { IsActive: true })
        {
            throw ValidationException.ForField("handlingDepartmentId", InactiveDepartmentMessage);
        }

        return department.HandlerCount == 0 ? [NoActiveHandlersWarning(department.Name)] : [];
    }

    private static int? HandlingDepartmentOf(RoutingType routingType, int? departmentId) =>
        routingType == RoutingType.FixedDepartment ? departmentId : null;

    private async Task SaveTranslatingDuplicatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.IndexName == UniqueIndexes.CategoryCode)
        {
            throw DuplicateCode();
        }
    }

    private async Task<CategoryDto> GetDtoAsync(int categoryId, CancellationToken cancellationToken) =>
        await _categories.GetDtoAsync(categoryId, cancellationToken) ?? throw new NotFoundException(NotFoundTitle);

    private void Audit(string action, int categoryId, string ipAddress) =>
        _auditLogs.Add(AuditEntries.ForEntity(action, AuditEntityTypes.Category, categoryId,
            _currentUser.OrganizationId, _currentUser.UserId, ipAddress, _clock.UtcNow));

    private void EvictCache() => _cache.Invalidate(ReferenceData.Categories, _currentUser.OrganizationId);

    private static ConflictException DuplicateCode() =>
        new(DuplicateCodeMessage, "קוד הקטגוריה קבוע ומשמש לסיווג ולניתוב.", "duplicate-category-code",
            new Dictionary<string, string[]> { ["code"] = [DuplicateCodeMessage] });
}
