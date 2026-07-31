namespace PORMS.API.Contracts;

public sealed class AssignTaskRequest
{
    public Guid? AssignedUserId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
}

public sealed class CompleteTaskRequest
{
    public string? CompletionNote { get; set; }
}

public sealed class TaskAssigneeResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? PortId { get; set; }
    public string? PortName { get; set; }
}
