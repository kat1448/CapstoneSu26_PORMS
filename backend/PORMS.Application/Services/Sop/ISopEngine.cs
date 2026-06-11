using PORMS.Domain.Events;

namespace PORMS.Application.Services.Sop;

public interface ISopEngine
{
    Task HandleRiskChangedAsync(RiskChangedEvent riskChangedEvent, CancellationToken cancellationToken = default);
}
