import {
  getSimulationSnapshot as getSimulationSnapshotData,
  resetDemoData,
  runDemoStepSequence
} from "../mock/demoData";
import { requestJson, withMockFallback } from "./api";
import type { SimulationSnapshot } from "../types/simulation";

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
