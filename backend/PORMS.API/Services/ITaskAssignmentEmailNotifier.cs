using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Services;

public interface ITaskAssignmentEmailNotifier
{
    Task SendAssignedTaskEmailAsync(TaskLogReadModel task, CancellationToken cancellationToken);
}
