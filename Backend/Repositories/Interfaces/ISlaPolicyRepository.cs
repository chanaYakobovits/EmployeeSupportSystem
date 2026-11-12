using Common.Enums;
using Common.Models;

namespace Repositories.Interfaces;

/// <summary>SLA policies of the signed-in user's organization (organization query filter), one per urgency.</summary>
public interface ISlaPolicyRepository
{
    /// <summary>Resolution hours by urgency. A level without a row is missing from the dictionary.</summary>
    Task<IReadOnlyDictionary<Urgency, int>> GetHoursAsync(CancellationToken cancellationToken);

    /// <summary>Tracked rows of the organization.</summary>
    Task<IReadOnlyList<SlaPolicy>> GetForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Stages a new policy; written by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(SlaPolicy policy);
}
