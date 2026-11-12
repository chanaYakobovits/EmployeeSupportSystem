using Common.Constants;
using Common.DTOs.Sla;
using Common.Enums;
using Common.Interfaces;
using Common.Models;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using Services.Interfaces;
using Services.Validators;

namespace Services.Implementations;

/// <summary>
/// SLA targets (spec 3.4): calendar hours per urgency, changed by the admin as one form. A change applies to new
/// tickets and to tickets whose urgency changes (task 3.3); existing DueAt values are deliberately left alone.
/// </summary>
public sealed class SlaPolicyService : ISlaPolicyService
{
    private readonly ISlaPolicyRepository _policies;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateSlaPoliciesRequest> _validator;
    private readonly ILogger<SlaPolicyService> _logger;

    public SlaPolicyService(
        ISlaPolicyRepository policies,
        IAuditLogRepository auditLogs,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        ICurrentUser currentUser,
        IValidator<UpdateSlaPoliciesRequest> validator,
        ILogger<SlaPolicyService> logger)
    {
        _policies = policies;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _validator = validator;
        _logger = logger;
    }

    public async Task<SlaPoliciesDto> GetAsync(CancellationToken cancellationToken)
    {
        var hours = await _policies.GetHoursAsync(cancellationToken);
        int Of(Urgency urgency) => hours.TryGetValue(urgency, out var value) ? value : SlaDefaults.Hours[urgency];
        return new SlaPoliciesDto(Of(Urgency.Critical), Of(Urgency.High), Of(Urgency.Medium), Of(Urgency.Low));
    }

    public async Task<SlaPoliciesDto> UpdateAsync(UpdateSlaPoliciesRequest request, string ipAddress,
        CancellationToken cancellationToken)
    {
        await _validator.EnsureValidAsync(request, cancellationToken);

        var target = new Dictionary<Urgency, int>
        {
            [Urgency.Critical] = request.Critical!.Value,
            [Urgency.High] = request.High!.Value,
            [Urgency.Medium] = request.Medium!.Value,
            [Urgency.Low] = request.Low!.Value,
        };

        var existing = (await _policies.GetForUpdateAsync(cancellationToken)).ToDictionary(p => p.Urgency);
        foreach (var (urgency, hours) in target)
        {
            if (existing.TryGetValue(urgency, out var policy))
            {
                policy.ResolutionHours = hours;
            }
            else
            {
                _policies.Add(new SlaPolicy
                {
                    OrganizationId = _currentUser.OrganizationId,
                    Urgency = urgency,
                    ResolutionHours = hours,
                });
            }
        }

        _auditLogs.Add(AuditEntries.ForEntity(AuditActions.SlaPoliciesUpdated, AuditEntityTypes.SlaPolicy, entityId: null,
            _currentUser.OrganizationId, _currentUser.UserId, ipAddress, _clock.UtcNow));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("SLA policies updated by admin {AdminUserId}", _currentUser.UserId);
        return new SlaPoliciesDto(target[Urgency.Critical], target[Urgency.High], target[Urgency.Medium], target[Urgency.Low]);
    }
}
