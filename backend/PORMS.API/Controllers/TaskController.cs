using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AdminOrSuperAdmin")]
[Route("api/tasks")]
public sealed class TaskController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskLogResponse>>> GetTasks(
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var tasks = await repository.GetTasksAsync(cancellationToken);
        return Ok(tasks.Select(ToResponse).ToList());
    }

    private static TaskLogResponse ToResponse(TaskLogReadModel task) =>
        new()
        {
            TaskId = task.TaskId,
            TaskCode = task.TaskCode,
            PortId = task.PortId,
            PortCode = task.PortCode,
            PortName = task.PortName,
            ZoneId = task.ZoneId,
            ZoneName = task.ZoneName,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Status = task.Status,
            AssignedUserId = task.AssignedUserId,
            AssignedUserName = task.AssignedUserName,
            AssignedTeam = task.AssignedTeam,
            DueAt = task.DueAt,
            SimulationSessionId = task.SimulationSessionId,
            IsSimulation = task.SimulationSessionId is not null,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
}
