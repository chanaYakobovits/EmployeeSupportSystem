using Common.Constants;
using Common.DTOs.Auth;
using Common.DTOs.Categories;
using Common.DTOs.Departments;
using Common.DTOs.Users;
using Common.Enums;
using Common.Models;
using Repositories.Interfaces;
using Services.Tests.Auth.Fakes;

namespace Services.Tests.Admin.Fakes;

/// <summary>In-memory departments over the same users as <see cref="FakeUserRepository"/>.</summary>
public sealed class FakeDepartmentRepository(FakeUserRepository users) : IDepartmentRepository
{
    public Dictionary<int, Department> Departments => users.Departments;

    /// <summary>Department ids that an active category routes to (Category.HandlingDepartmentId).</summary>
    public HashSet<int> HandlingDepartmentIds { get; } = [];

    public Task<IReadOnlyList<DepartmentDto>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DepartmentDto>>(Departments.Values
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.Name)
            .Select(ToDto)
            .ToList());

    public Task<DepartmentDto?> GetDtoAsync(int departmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Departments.TryGetValue(departmentId, out var d) ? ToDto(d) : null);

    public Task<Department?> FindForUpdateAsync(int departmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Departments.GetValueOrDefault(departmentId));

    public Task<string?> GetActiveNameAsync(int departmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Departments.TryGetValue(departmentId, out var d) && d.IsActive ? d.Name : null);

    public Task<bool> NameExistsAsync(string name, int? excludeDepartmentId, CancellationToken cancellationToken) =>
        Task.FromResult(Departments.Values.Any(d =>
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) && d.Id != excludeDepartmentId));

    public Task<Department?> FindManagedByAsync(int userId, CancellationToken cancellationToken) =>
        Task.FromResult(Departments.Values.FirstOrDefault(d => d.ManagerUserId == userId));

    public Task<int> CountActiveUsersAsync(int departmentId, CancellationToken cancellationToken) =>
        Task.FromResult(users.Users.Count(u => u.DepartmentId == departmentId && u.IsActive));

    public Task<bool> IsHandlingDepartmentOfActiveCategoryAsync(int departmentId, CancellationToken cancellationToken) =>
        Task.FromResult(HandlingDepartmentIds.Contains(departmentId));

    public void Add(Department department)
    {
        department.Id = Departments.Count == 0 ? 1 : Departments.Keys.Max() + 1;
        Departments[department.Id] = department;
    }

    private DepartmentDto ToDto(Department d)
    {
        var manager = users.Users.SingleOrDefault(u => u.Id == d.ManagerUserId);
        var members = users.Users.Where(u => u.DepartmentId == d.Id && u.IsActive).ToList();
        return new DepartmentDto(d.Id, d.Name, d.IsActive,
            manager is null ? null : new UserSummaryDto(manager.Id, manager.EmployeeCode, manager.FirstName, manager.LastName),
            members.Count, members.Count(u => u.Roles.Any(r => r.Role == Role.Handler)));
    }
}

/// <summary>Records which handlers' tickets were returned to the queue.</summary>
public sealed class FakeTicketRepository : ITicketRepository
{
    /// <summary>Unresolved tickets assigned to each handler.</summary>
    public Dictionary<int, int> UnresolvedByHandler { get; } = [];

    public List<int> UnassignedHandlers { get; } = [];

    public Task<int> UnassignUnresolvedTicketsAsync(int handlerUserId, DateTime now, CancellationToken cancellationToken)
    {
        UnassignedHandlers.Add(handlerUserId);
        var count = UnresolvedByHandler.GetValueOrDefault(handlerUserId);
        UnresolvedByHandler.Remove(handlerUserId);
        return Task.FromResult(count);
    }
}

/// <summary>In-memory categories; the handling department's name comes from <see cref="FakeDepartmentRepository"/>.</summary>
public sealed class FakeCategoryRepository(FakeDepartmentRepository departments) : ICategoryRepository
{
    public Dictionary<int, Category> Categories { get; } = [];

    /// <summary>How many times the list was read from the "database" (to observe the cache).</summary>
    public int GetAllCount { get; private set; }

    public Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        GetAllCount++;
        return Task.FromResult<IReadOnlyList<CategoryDto>>(Categories.Values
            .OrderBy(c => c.IsFallback).ThenBy(c => c.Name)
            .Select(ToDto)
            .ToList());
    }

    public Task<CategoryDto?> GetDtoAsync(int categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.TryGetValue(categoryId, out var c) ? ToDto(c) : null);

    public Task<Category?> FindForUpdateAsync(int categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.GetValueOrDefault(categoryId));

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.Values.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)));

    public void Add(Category category)
    {
        category.Id = Categories.Count == 0 ? 1 : Categories.Keys.Max() + 1;
        Categories[category.Id] = category;
    }

    private CategoryDto ToDto(Category c) =>
        new(c.Id, c.Code, c.Name, c.Description, c.RoutingType,
            c.HandlingDepartmentId is { } id && departments.Departments.TryGetValue(id, out var d)
                ? new DepartmentSummaryDto(d.Id, d.Name)
                : null,
            c.IsFallback, c.IsActive);
}

/// <summary>In-memory SLA policies, seeded with the defaults of spec 3.4.</summary>
public sealed class FakeSlaPolicyRepository : ISlaPolicyRepository
{
    public List<SlaPolicy> Policies { get; } = SlaDefaults.Hours
        .Select((p, i) => new SlaPolicy { Id = i + 1, OrganizationId = 1, Urgency = p.Key, ResolutionHours = p.Value })
        .ToList();

    public Task<IReadOnlyDictionary<Urgency, int>> GetHoursAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Urgency, int>>(Policies.ToDictionary(p => p.Urgency, p => p.ResolutionHours));

    public Task<IReadOnlyList<SlaPolicy>> GetForUpdateAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SlaPolicy>>(Policies);

    public void Add(SlaPolicy policy) => Policies.Add(policy);
}
