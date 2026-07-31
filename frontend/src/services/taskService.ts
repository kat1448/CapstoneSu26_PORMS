import { requestJson } from "./api";

export type TaskLogRecord = {
  acknowledgedAt?: string | null;
  acknowledgedByUserId?: string | null;
  alertId?: string | null;
  assignedTeam?: string | null;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
  completedAt?: string | null;
  completedByUserId?: string | null;
  completionNote?: string | null;
  createdAt: string;
  description?: string | null;
  dueAt?: string | null;
  isSimulation: boolean;
  portCode: string;
  portId: string;
  portName: string;
  priority: "LOW" | "MEDIUM" | "HIGH" | "CRITICAL" | string;
  simulationSessionId?: string | null;
  status: "NEW" | "ACKNOWLEDGED" | "IN_PROGRESS" | "COMPLETED" | "CANCELLED" | string;
  taskCode: string;
  taskId: string;
  title: string;
  updatedAt: string;
  startedAt?: string | null;
  zoneId?: string | null;
  zoneName?: string | null;
};

export type TaskAssignee = {
  email: string;
  fullName: string;
  portId?: string | null;
  portName?: string | null;
  role: string;
  userId: string;
};

export type AssignTaskInput = {
  assignedUserId?: string | null;
  dueAt?: string | null;
};

export type CompleteTaskInput = {
  completionNote?: string | null;
};

export async function getTasks(): Promise<TaskLogRecord[]> {
  return requestJson<TaskLogRecord[]>("/api/tasks");
}

export async function getTask(taskId: string): Promise<TaskLogRecord> {
  return requestJson<TaskLogRecord>(`/api/tasks/${taskId}`);
}

export async function getTaskAssignees(): Promise<TaskAssignee[]> {
  return requestJson<TaskAssignee[]>("/api/tasks/assignees");
}

export async function assignTask(taskId: string, input: AssignTaskInput): Promise<TaskLogRecord> {
  return requestJson<TaskLogRecord>(`/api/tasks/${taskId}/assignment`, {
    method: "PATCH",
    body: JSON.stringify(input)
  });
}

export async function acknowledgeTask(taskId: string): Promise<TaskLogRecord> {
  return requestJson<TaskLogRecord>(`/api/tasks/${taskId}/acknowledge`, {
    method: "PATCH"
  });
}

export async function startTask(taskId: string): Promise<TaskLogRecord> {
  return requestJson<TaskLogRecord>(`/api/tasks/${taskId}/start`, {
    method: "PATCH"
  });
}

export async function completeTask(taskId: string, input: CompleteTaskInput): Promise<TaskLogRecord> {
  return requestJson<TaskLogRecord>(`/api/tasks/${taskId}/complete`, {
    method: "PATCH",
    body: JSON.stringify(input)
  });
}
