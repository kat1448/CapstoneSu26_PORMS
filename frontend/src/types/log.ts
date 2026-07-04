export type OperationEvent = {
  actorName: string;
  entityType: string;
  eventType: string;
  isSimulation?: boolean;
  occurredAt: string;
  occurredAtRaw: string;
  operationEventId: string;
  portCode: string;
  portId?: string | null;
  portName?: string | null;
  simulationSessionId?: string | null;
  summary: string;
  tone: "info" | "warning" | "danger" | "success";
  zoneId?: string | null;
  zoneName?: string | null;
};
