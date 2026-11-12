using Common.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Scoped: one per request / background scope. The signed-in user's organization wins; without a user, the
/// organization a background job entered; otherwise null (the query filters then return nothing).
/// </summary>
public sealed class OrganizationContext(ICurrentUser currentUser) : IOrganizationContext, ISystemOrganizationContext
{
    private int? _enteredOrganizationId;

    public int? OrganizationId =>
        currentUser.IsAuthenticated ? currentUser.OrganizationId : _enteredOrganizationId;

    public void Enter(int organizationId)
    {
        if (currentUser.IsAuthenticated)
        {
            throw new InvalidOperationException(
                "A signed-in user's scope cannot switch organization; Enter is for background jobs only.");
        }

        if (_enteredOrganizationId is { } entered && entered != organizationId)
        {
            throw new InvalidOperationException(
                "This scope is already bound to another organization; open a new scope per organization.");
        }

        _enteredOrganizationId = organizationId;
    }
}
