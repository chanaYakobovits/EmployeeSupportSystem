using Common.Models;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class AuditLogRepository(AppDbContext db) : IAuditLogRepository
{
    public void Add(AuditLog auditLog) => db.AuditLogs.Add(auditLog);
}
