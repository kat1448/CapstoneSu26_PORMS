using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public interface IAlertEmailNotifier
{
    Task SendHighSeverityAlertAsync(AlertNotificationReadModel alert, CancellationToken cancellationToken);
}
