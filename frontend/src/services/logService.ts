import { getOperationEvents as getOperationEventsData } from "../mock/demoData";
import { formatTimeLabel, requestJson, withMockFallback } from "./api";
import type { OperationEvent } from "../types/log";

type OperationEventApiResponse = Omit<OperationEvent, "actorName" | "entityType" | "occurredAt" | "portCode"> & {
  actorName: string | null;
  entityType: string | null;
  occurredAt: string;
  portCode: string | null;
};

export type OperationEventScope = "live" | "simulation";

export async function getOperationEvents(scope: OperationEventScope = "live"): Promise<OperationEvent[]> {
  const normalize = (events: OperationEventApiResponse[]) => events.map((event) => ({
    ...event,
    actorName: event.actorName ?? "SYSTEM",
    entityType: event.entityType ?? "system",
    occurredAt: formatTimeLabel(event.occurredAt),
    portCode: event.portCode ?? "N/A"
  }));

  return withMockFallback(
    async () => {
      const path = scope === "simulation" ? "/api/operation-events?scope=simulation" : "/api/operation-events";
      const events = await requestJson<OperationEventApiResponse[]>(path);
      return normalize(events);
    },
    () => normalize(getOperationEventsData())
  );
}
