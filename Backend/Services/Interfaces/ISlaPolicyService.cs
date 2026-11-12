using Common.DTOs.Sla;

namespace Services.Interfaces;

/// <summary>SLA resolution targets of the signed-in user's organization (spec 3.4, 9.3).</summary>
public interface ISlaPolicyService
{
    /// <summary>Any signed-in user. A level without a row gets the default of spec 3.4.</summary>
    Task<SlaPoliciesDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Admin. All four levels together. Applies to new tickets and to tickets whose urgency changes;
    /// the DueAt of existing tickets is not recalculated.
    /// </summary>
    /// <exception cref="Common.Exceptions.ValidationException">Not whole positive hours, or out of order.</exception>
    Task<SlaPoliciesDto> UpdateAsync(UpdateSlaPoliciesRequest request, string ipAddress, CancellationToken cancellationToken);
}
