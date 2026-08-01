import {
  getSimulationSnapshot as getSimulationSnapshotData,
  resetDemoData,
  runDemoStepSequence
} from "../mock/demoData";
import { requestJson, requestVoid, withMockFallback } from "./api";
import type {
  CreateSimulationDatasetInput,
  ForecastPlan,
  ForecastHorizonDays,
  SimulationDatasetDetail,
  SimulationDatasetSummary,
  SimulationResult,
  SimulationRunResult,
  SimulationSnapshot
} from "../types/simulation";

export async function getSimulationSnapshot(): Promise<SimulationSnapshot> {
  return withMockFallback(
    () => requestJson<SimulationSnapshot>("/api/simulation/current"),
    () => getSimulationSnapshotData()
  );
}

export async function runDemoSimulation(): Promise<SimulationRunResult | null> {
  return withMockFallback(
    async () => requestJson<SimulationRunResult>("/api/simulation/run-demo", {
        method: "POST",
        body: JSON.stringify({ portCode: "DNTSA" })
      }),
    async () => {
      await runDemoStepSequence();
      return null;
    }
  );
}

export async function restartDemoSimulation(): Promise<void> {
  await withMockFallback(
    async () => {
      await requestJson("/api/simulation/run-demo", {
        method: "POST",
        body: JSON.stringify({ portCode: "DNTSA" })
      });
    },
    async () => {
      resetDemoData();
      await runDemoStepSequence();
    }
  );
}

export function getSimulationDatasets(): Promise<SimulationDatasetSummary[]> {
  return requestJson<SimulationDatasetSummary[]>("/api/simulation/datasets");
}

export function getSimulationDataset(datasetId: string): Promise<SimulationDatasetDetail> {
  return requestJson<SimulationDatasetDetail>(`/api/simulation/datasets/${datasetId}`);
}

export function getSimulationMapPoints(): Promise<SimulationResult["mapPoints"]> {
  return requestJson<SimulationResult["mapPoints"]>("/api/simulation/map-points");
}

export function createSimulationDataset(input: CreateSimulationDatasetInput): Promise<SimulationDatasetSummary> {
  return requestJson<SimulationDatasetSummary>("/api/simulation/datasets", {
    body: JSON.stringify(toDatasetPayload(input)),
    method: "POST"
  });
}

export function updateSimulationDataset(datasetId: string, input: CreateSimulationDatasetInput): Promise<SimulationDatasetSummary> {
  return requestJson<SimulationDatasetSummary>(`/api/simulation/datasets/${datasetId}`, {
    body: JSON.stringify(toDatasetPayload(input)),
    method: "PUT"
  });
}

export function deleteSimulationDataset(datasetId: string): Promise<void> {
  return requestVoid(`/api/simulation/datasets/${datasetId}`, { method: "DELETE" });
}

export function createForecastPlan(input: { horizonDays: ForecastHorizonDays; portCode: string }): Promise<ForecastPlan> {
  return requestJson<ForecastPlan>("/api/simulation/forecast-plan", {
    body: JSON.stringify(input),
    method: "POST"
  });
}

function toDatasetPayload(input: CreateSimulationDatasetInput): CreateSimulationDatasetInput {
  const payload: CreateSimulationDatasetInput = {
    ...input,
    snapshots: input.snapshots.map((snapshot) => ({
      ...snapshot,
      beaufortNumber: toBeaufort(snapshot.windSpeedMs),
      zoneId: snapshot.zoneId?.trim() ? snapshot.zoneId.trim() : null
    }))
  };

  return payload;
}

function toBeaufort(windSpeedMs: number): number {
  if (windSpeedMs < 0.3) return 0;
  if (windSpeedMs < 1.6) return 1;
  if (windSpeedMs < 3.4) return 2;
  if (windSpeedMs < 5.5) return 3;
  if (windSpeedMs < 8) return 4;
  if (windSpeedMs < 10.8) return 5;
  if (windSpeedMs < 13.9) return 6;
  if (windSpeedMs < 17.2) return 7;
  if (windSpeedMs < 20.8) return 8;
  if (windSpeedMs < 24.5) return 9;
  if (windSpeedMs < 28.5) return 10;
  if (windSpeedMs < 32.7) return 11;
  return 12;
}

export function runSimulationDataset(datasetId: string): Promise<SimulationRunResult> {
  return requestJson<SimulationRunResult>("/api/simulation/run", {
    body: JSON.stringify({ datasetId }),
    method: "POST"
  });
}

export function getSimulationResult(sessionId: string): Promise<SimulationResult> {
  return requestJson<SimulationResult>(`/api/simulation/${sessionId}/result`);
}
