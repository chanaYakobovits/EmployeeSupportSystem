using Common.Enums;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Data;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public sealed class SlaPolicyRepository(AppDbContext db) : ISlaPolicyRepository
{
    public async Task<IReadOnlyDictionary<Urgency, int>> GetHoursAsync(CancellationToken cancellationToken) =>
        await db.SlaPolicies.AsNoTracking()
            .Select(s => new { s.Urgency, s.ResolutionHours })
            .ToDictionaryAsync(s => s.Urgency, s => s.ResolutionHours, cancellationToken);

    public async Task<IReadOnlyList<SlaPolicy>> GetForUpdateAsync(CancellationToken cancellationToken) =>
        await db.SlaPolicies.ToListAsync(cancellationToken);

    public void Add(SlaPolicy policy) => db.SlaPolicies.Add(policy);
}
