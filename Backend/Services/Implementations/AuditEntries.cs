using Common.Constants;
using Common.Models;

namespace Services.Implementations;

/// <summary>Audit records of administrative actions (spec 6.4): who did what to which entity. Never any content.</summary>
internal static class AuditEntries
{
    public static AuditLog ForUser(string action, int organizationId, int actorUserId, int targetUserId,
        string ipAddress, DateTime now) =>
        ForEntity(action, AuditEntityTypes.User, targetUserId, organizationId, actorUserId, ipAddress, now);

    /// <param name="entityId">Null for an organization-wide setting (e.g. the SLA policies as a whole).</param>
    public static AuditLog ForEntity(string action, string entityType, int? entityId, int organizationId,
        int actorUserId, string ipAddress, DateTime now) => new()
    {
        OrganizationId = organizationId,
        UserId = actorUserId,
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        IpAddress = ipAddress,
        CreatedAt = now,
    };
}
