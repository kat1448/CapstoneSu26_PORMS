using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PORMS.API.Contracts;
using PORMS.API.Services;
using PORMS.Infrastructure.Repositories;

namespace PORMS.API.Controllers;

[ApiController]
[Authorize(Policy = "AllAppUsers")]
[Route("api/tasks")]
public sealed class TaskController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskLogResponse>>> GetTasks(
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();
        var tasks = await repository.GetTasksAsync(access.UserId.Value, access.Role, access.PortId, cancellationToken);
        return Ok(tasks.Select(ToResponse).ToList());
    }

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<TaskLogResponse>> GetTask(
        Guid taskId,
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();
        var task = await repository.GetTaskAsync(taskId, access.UserId.Value, access.Role, cancellationToken);
        return task is null ? NotFound() : Ok(ToResponse(task));
    }

    [HttpGet("assignees")]
    [Authorize(Policy = "AdminOrPortManager")]
    public async Task<ActionResult<IReadOnlyList<TaskAssigneeResponse>>> GetAssignees(
        [FromServices] TaskRepository repository,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();
        var assignees = await repository.GetAssignableUsersAsync(
            access.UserId.Value, access.Role, access.PortId, cancellationToken);
        return Ok(assignees.Select(assignee => new TaskAssigneeResponse
        {
            UserId = assignee.UserId,
            FullName = assignee.FullName,
            Email = assignee.Email,
            Role = assignee.Role,
            PortId = assignee.PortId,
            PortName = assignee.PortName
        }).ToList());
    }

    [HttpPatch("{taskId:guid}/assignment")]
    [Authorize(Policy = "AdminOrPortManager")]
    public async Task<ActionResult<TaskLogResponse>> AssignTask(
        Guid taskId,
        [FromBody] AssignTaskRequest request,
        [FromServices] TaskRepository repository,
        [FromServices] OperationEventRepository eventRepository,
        [FromServices] ITaskAssignmentEmailNotifier emailNotifier,
        [FromServices] ILogger<TaskController> logger,
        CancellationToken cancellationToken)
    {
        var actorUserId = GetUserId(User);
        if (actorUserId is null) return Unauthorized();

        var access = GetAccessScope(User);
        var currentTask = await repository.GetTaskAsync(taskId, actorUserId.Value, access.Role, cancellationToken);
        if (currentTask is null) return NotFound();
        if (currentTask.Status != "NEW")
        {
            return Conflict(new ErrorResponse { Error = "Chỉ có thể phân công nhiệm vụ đang chờ tiếp nhận." });
        }

        var task = await repository.AssignTaskAsync(
            taskId,
            request.AssignedUserId,
            null,
            request.DueAt,
            actorUserId.Value,
            cancellationToken);

        if (task is null)
        {
            return BadRequest(new ErrorResponse { Error = "Người được chọn phải là Operator đang hoạt động và thuộc cùng cảng." });
        }

        await TryRecordTaskEventAsync(
            eventRepository,
            logger,
            task,
            actorUserId.Value,
            task.AssignedUserId is null ? "TASK_UNASSIGNED" : "TASK_ASSIGNED",
            task.AssignedUserId is null
                ? $"Đã thu hồi phân công nhiệm vụ {task.TaskCode}."
                : $"Đã phân công nhiệm vụ {task.TaskCode} cho {task.AssignedUserName}.",
            cancellationToken);

        if (task.AssignedUserId is not null)
        {
            try
            {
                await emailNotifier.SendAssignedTaskEmailAsync(task, cancellationToken);
                Response.Headers["X-PORMS-Email-Delivery"] = "processed";
            }
            catch (Exception exception)
            {
                // The assignment has already been saved successfully. Email is a secondary
                // delivery channel, so an SMTP outage must not make the user repeat the action.
                logger.LogError(
                    exception,
                    "Task assignment saved but email delivery failed. TaskId={TaskId}, AssignedUserId={AssignedUserId}",
                    task.TaskId,
                    task.AssignedUserId);
                Response.Headers["X-PORMS-Email-Delivery"] = "failed";
            }
        }

        return Ok(ToResponse(task));
    }

    [HttpPatch("{taskId:guid}/acknowledge")]
    public async Task<ActionResult<TaskLogResponse>> AcknowledgeTask(
        Guid taskId,
        [FromServices] TaskRepository repository,
        [FromServices] OperationEventRepository eventRepository,
        [FromServices] ILogger<TaskController> logger,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();
        var currentTask = await repository.GetTaskAsync(taskId, access.UserId.Value, access.Role, cancellationToken);
        if (currentTask is null) return NotFound();
        if (currentTask.AssignedUserId is null)
            return Conflict(new ErrorResponse { Error = "Nhiệm vụ chưa được phân công." });
        if (currentTask.Status != "NEW")
            return Conflict(new ErrorResponse { Error = "Chỉ nhiệm vụ đang chờ tiếp nhận mới có thể được tiếp nhận." });

        var task = await repository.AcknowledgeTaskAsync(taskId, access.UserId, cancellationToken);
        if (task is null) return Forbid();
        await TryRecordTaskEventAsync(
            eventRepository,
            logger,
            task, access.UserId.Value, "TASK_ACKNOWLEDGED",
            $"Đã tiếp nhận nhiệm vụ {task.TaskCode}.", cancellationToken);
        return Ok(ToResponse(task));
    }

    [HttpPatch("{taskId:guid}/start")]
    public async Task<ActionResult<TaskLogResponse>> StartTask(
        Guid taskId,
        [FromServices] TaskRepository repository,
        [FromServices] OperationEventRepository eventRepository,
        [FromServices] ILogger<TaskController> logger,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();
        var currentTask = await repository.GetTaskAsync(taskId, access.UserId.Value, access.Role, cancellationToken);
        if (currentTask is null) return NotFound();
        if (currentTask.Status != "ACKNOWLEDGED")
            return Conflict(new ErrorResponse { Error = "Hãy tiếp nhận nhiệm vụ trước khi bắt đầu thực hiện." });

        var task = await repository.StartTaskAsync(taskId, access.UserId, cancellationToken);
        if (task is null) return Forbid();
        await TryRecordTaskEventAsync(
            eventRepository,
            logger,
            task, access.UserId.Value, "TASK_STARTED",
            $"Đã bắt đầu thực hiện nhiệm vụ {task.TaskCode}.", cancellationToken);
        return Ok(ToResponse(task));
    }

    [HttpPatch("{taskId:guid}/complete")]
    public async Task<ActionResult<TaskLogResponse>> CompleteTask(
        Guid taskId,
        [FromBody] CompleteTaskRequest request,
        [FromServices] TaskRepository repository,
        [FromServices] OperationEventRepository eventRepository,
        [FromServices] ILogger<TaskController> logger,
        CancellationToken cancellationToken)
    {
        var access = GetAccessScope(User);
        if (access.UserId is null) return Unauthorized();
        var completionNote = request.CompletionNote?.Trim();
        if (string.IsNullOrWhiteSpace(completionNote) || completionNote.Length < 10)
        {
            return BadRequest(new ErrorResponse { Error = "Kết quả xử lý phải có ít nhất 10 ký tự." });
        }

        var currentTask = await repository.GetTaskAsync(taskId, access.UserId.Value, access.Role, cancellationToken);
        if (currentTask is null) return NotFound();
        if (currentTask.Status != "IN_PROGRESS")
            return Conflict(new ErrorResponse { Error = "Chỉ nhiệm vụ đang thực hiện mới có thể hoàn tất." });

        var task = await repository.CompleteTaskAsync(
            taskId,
            access.UserId,
            completionNote,
            cancellationToken);

        if (task is null) return Forbid();
        await TryRecordTaskEventAsync(
            eventRepository,
            logger,
            task, access.UserId.Value, "TASK_COMPLETED",
            $"Đã hoàn tất nhiệm vụ {task.TaskCode}: {completionNote}", cancellationToken);
        return Ok(ToResponse(task));
    }

    private static async Task TryRecordTaskEventAsync(
        OperationEventRepository eventRepository,
        ILogger<TaskController> logger,
        TaskLogReadModel task,
        Guid actorUserId,
        string eventType,
        string summary,
        CancellationToken cancellationToken)
    {
        try
        {
            await eventRepository.RecordTaskEventAsync(
                task,
                actorUserId,
                eventType,
                summary,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Task workflow audit event failed. TaskId={TaskId}, EventType={EventType}",
                task.TaskId,
                eventType);
        }
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

    private static (Guid? UserId, string Role, Guid? PortId) GetAccessScope(ClaimsPrincipal user)
    {
        var rawPortId = user.FindFirstValue("port_id");
        return (
            GetUserId(user),
            user.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            Guid.TryParse(rawPortId, out var portId) ? portId : null);
    }
}
