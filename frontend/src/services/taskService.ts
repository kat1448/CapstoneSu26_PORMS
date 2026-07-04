import { requestJson } from "./api";

export type TaskLogRecord = {
  assignedTeam?: string | null;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
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
  zoneId?: string | null;
  zoneName?: string | null;
};

export async function getTasks(): Promise<TaskLogRecord[]> {
  return requestJson<TaskLogRecord[]>("/api/tasks");
}
