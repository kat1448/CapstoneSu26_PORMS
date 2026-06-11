using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PORMS.Application.Common.Events;
using PORMS.Application.Services.Sop;
using PORMS.Domain.Events;

namespace PORMS.Infrastructure.Events;

public sealed class SopDomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SopDomainEventPublisher> _logger;

    public SopDomainEventPublisher(
        IServiceScopeFactory scopeFactory,
        ILogger<SopDomainEventPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        _logger.LogInformation("Domain event published: {EventType} {@Event}", typeof(TEvent).Name, domainEvent);

        if (domainEvent is not RiskChangedEvent riskChangedEvent)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var sopEngine = scope.ServiceProvider.GetRequiredService<ISopEngine>();
        await sopEngine.HandleRiskChangedAsync(riskChangedEvent, cancellationToken);
    }
}
