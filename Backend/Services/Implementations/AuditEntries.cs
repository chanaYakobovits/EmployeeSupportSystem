using Common.Constants;
using Common.Models;

namespace Services.Implementations;

/// <summary>Audit records of administrative actions (spec 6.4): who did what to which user. Never any content.</summary>
internal static class AuditEntries
{
    public static AuditLog ForUser(string action, int organizationId, int actorUserId, int targetUserId,
        string ipAddress, DateTime now) => new()
    {
        OrganizationId = organizationId,
        UserId = actorUserId,
        Action = action,
        EntityType = AuditEntityTypes.User,
        EntityId = targetUserId,
        IpAddress = ipAddress,
        CreatedAt = now,
    };
}
