using Microsoft.Extensions.Logging;
using PORMS.Application.Common.Events;

namespace PORMS.Infrastructure.Events;

public sealed class LoggingDomainEventPublisher : IDomainEventPublisher
{
    private readonly ILogger<LoggingDomainEventPublisher> _logger;

    public LoggingDomainEventPublisher(ILogger<LoggingDomainEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        _logger.LogInformation("Domain event published: {EventType} {@Event}", typeof(TEvent).Name, domainEvent);
        return Task.CompletedTask;
    }
}
