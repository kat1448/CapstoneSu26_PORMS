export type OperationEvent = {
  actorName: string;
  entityType: string;
  eventType: string;
  isSimulation?: boolean;
  occurredAt: string;
  operationEventId: string;
  portCode: string;
  simulationSessionId?: string | null;
  summary: string;
  tone: "info" | "warning" | "danger" | "success";
};
