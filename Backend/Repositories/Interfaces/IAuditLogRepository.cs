using Common.Models;

namespace Repositories.Interfaces;

public interface IAuditLogRepository
{
    /// <summary>Stages the record; it is written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(AuditLog auditLog);
}
