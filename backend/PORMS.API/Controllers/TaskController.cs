using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AllAppUsers")]
[Route("api/tasks")]
public sealed class TaskController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<IReadOnlyList<TaskLogResponse>>> GetTasks(
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var tasks = await repository.GetTasksAsync(cancellationToken);
        return Ok(tasks.Select(ToResponse).ToList());
    }

    [HttpGet("assignees")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<IReadOnlyList<TaskAssigneeResponse>>> GetAssignees(
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var assignees = await repository.GetAssignableUsersAsync(cancellationToken);
        return Ok(assignees.Select(assignee => new TaskAssigneeResponse
        {
            UserId = assignee.UserId,
            FullName = assignee.FullName,
            Email = assignee.Email,
            Role = assignee.Role
        }).ToList());
    }

    [HttpPatch("{taskId:guid}/assignment")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<TaskLogResponse>> AssignTask(
        Guid taskId,
        [FromBody] AssignTaskRequest request,
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var task = await repository.AssignTaskAsync(
            taskId,
            request.AssignedUserId,
            null,
            request.DueAt,
            cancellationToken);

        return task is null ? NotFound() : Ok(ToResponse(task));
    }

    [HttpPatch("{taskId:guid}/acknowledge")]
    public async Task<ActionResult<TaskLogResponse>> AcknowledgeTask(
        Guid taskId,
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var task = await repository.AcknowledgeTaskAsync(taskId, GetUserId(User), cancellationToken);
        return task is null ? NotFound() : Ok(ToResponse(task));
    }

    [HttpPatch("{taskId:guid}/start")]
    public async Task<ActionResult<TaskLogResponse>> StartTask(
        Guid taskId,
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var task = await repository.StartTaskAsync(taskId, cancellationToken);
        return task is null ? NotFound() : Ok(ToResponse(task));
    }

    [HttpPatch("{taskId:guid}/complete")]
    public async Task<ActionResult<TaskLogResponse>> CompleteTask(
        Guid taskId,
        [FromBody] CompleteTaskRequest request,
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var task = await repository.CompleteTaskAsync(
            taskId,
            GetUserId(User),
            request.CompletionNote,
            cancellationToken);

        return task is null ? NotFound() : Ok(ToResponse(task));
    }

    public static TaskLogResponse ToResponse(TaskLogReadModel task) =>
        new()
        {
            TaskId = task.TaskId,
            TaskCode = task.TaskCode,
            AlertId = task.AlertId,
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
            AcknowledgedByUserId = task.AcknowledgedByUserId,
            AcknowledgedAt = task.AcknowledgedAt,
            StartedAt = task.StartedAt,
            CompletedByUserId = task.CompletedByUserId,
            CompletedAt = task.CompletedAt,
            CompletionNote = task.CompletionNote,
            DueAt = task.DueAt,
            SimulationSessionId = task.SimulationSessionId,
            IsSimulation = task.SimulationSessionId is not null,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var rawUserId = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}
