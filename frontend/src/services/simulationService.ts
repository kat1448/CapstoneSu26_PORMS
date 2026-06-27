import {
  getSimulationSnapshot as getSimulationSnapshotData,
  resetDemoData,
  runDemoStepSequence
} from "../mock/demoData";
import { requestJson, withMockFallback } from "./api";
import type {
  CreateSimulationDatasetInput,
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

export async function runDemoSimulation(): Promise<void> {
  await withMockFallback(
    async () => {
      await requestJson("/api/simulation/run-demo", {
        method: "POST",
        body: JSON.stringify({ portCode: "DNTSA" })
      });
    },
    () => runDemoStepSequence()
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

export function getSimulationMapPoints(): Promise<SimulationResult["mapPoints"]> {
  return requestJson<SimulationResult["mapPoints"]>("/api/simulation/map-points");
}

export function createSimulationDataset(input: CreateSimulationDatasetInput): Promise<SimulationDatasetSummary> {
  const payload: CreateSimulationDatasetInput = {
    ...input,
    snapshots: input.snapshots.map((snapshot) => ({
      ...snapshot,
      zoneId: snapshot.zoneId?.trim() ? snapshot.zoneId.trim() : null
    }))
  };

  return requestJson<SimulationDatasetSummary>("/api/simulation/datasets", {
    body: JSON.stringify(payload),
    method: "POST"
  });
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
