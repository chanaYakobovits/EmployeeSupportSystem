namespace Common.Interfaces;

/// <summary>
/// The organization whose rows the global query filters return (spec 6.2). The signed-in user's organization during
/// an HTTP request; the organization a background job entered explicitly (<see cref="ISystemOrganizationContext"/>);
/// otherwise null – and then no filtered row is returned at all (fail closed).
/// </summary>
public interface IOrganizationContext
{
    int? OrganizationId { get; }
}

/// <summary>
/// For code that runs without a signed-in user (background jobs): binds the current DI scope to one organization,
/// so the query filters work exactly as in a request of that organization. One organization per scope – a job that
/// handles several organizations opens a scope for each (see API/BackgroundJobs).
/// Deliberately not a general "ignore the filters" switch.
/// </summary>
public interface ISystemOrganizationContext
{
    /// <exception cref="InvalidOperationException">
    /// A user is signed in in this scope, or the scope is already bound to another organization.
    /// </exception>
    void Enter(int organizationId);
}
