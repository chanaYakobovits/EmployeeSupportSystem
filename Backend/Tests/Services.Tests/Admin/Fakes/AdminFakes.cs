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
