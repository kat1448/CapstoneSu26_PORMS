export type OperationEvent = {
  actorName: string;
  entityType: string;
  eventType: string;
  occurredAt: string;
  operationEventId: string;
  portCode: string;
  summary: string;
  tone: "info" | "warning" | "danger" | "success";
};
