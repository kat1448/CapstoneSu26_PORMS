import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  createSimulationDataset,
  createForecastPlan,
  deleteSimulationDataset,
  getSimulationDataset,
  getSimulationDatasets,
  getSimulationMapPoints,
  getSimulationResult,
  runSimulationDataset,
  updateSimulationDataset
} from "./simulationService";
import type { CreateSimulationDatasetInput } from "../types/simulation";

describe("simulationService", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it("creates datasets and runs simulations through the real API", async () => {
    const input: CreateSimulationDatasetInput = {
      description: "Kich ban gio manh",
      name: "Bao test",
      portCode: "DNTSA",
      snapshots: [{
        beaufortNumber: 9,
        rainfall1hMm: 30,
        snapshotNumber: 1,
        visibilityKm: 3,
        windSpeedMs: 22,
        zoneId: "zone-1"
      }]
    };
    const dataset = { datasetId: "dataset-1", description: input.description, name: input.name, portCode: "DNTSA", snapshotCount: 1 };
    const run = { sessionId: "session-1", finalRiskLevel: "HIGH", generatedTaskCount: 1 };
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify([dataset]), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(dataset), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(run), { status: 200 }));

    await expect(getSimulationDatasets()).resolves.toEqual([dataset]);
    await expect(createSimulationDataset(input)).resolves.toEqual(dataset);
    await expect(runSimulationDataset("dataset-1")).resolves.toEqual(run);

    expect(fetch).toHaveBeenNthCalledWith(1, "http://localhost:5000/api/simulation/datasets", expect.any(Object));
    expect(fetch).toHaveBeenNthCalledWith(2, "http://localhost:5000/api/simulation/datasets", expect.objectContaining({
      body: JSON.stringify(input),
      method: "POST"
    }));
    expect(fetch).toHaveBeenNthCalledWith(3, "http://localhost:5000/api/simulation/run", expect.objectContaining({
      body: JSON.stringify({ datasetId: "dataset-1" }),
      method: "POST"
    }));
  });

  it("normalizes blank zone ids before creating a simulation dataset", async () => {
    const input: CreateSimulationDatasetInput = {
      description: "Khong chon zone",
      name: "Kich ban khong zone",
      portCode: "DNTSA",
      snapshots: [{
        beaufortNumber: 8,
        rainfall1hMm: 28,
        snapshotNumber: 1,
        visibilityKm: 4,
        windSpeedMs: 18,
        zoneId: ""
      }]
    };
    const dataset = { datasetId: "dataset-blank-zone", description: input.description, name: input.name, portCode: "DNTSA", snapshotCount: 1 };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(dataset), { status: 201 }));

    await expect(createSimulationDataset(input)).resolves.toEqual(dataset);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/simulation/datasets", expect.objectContaining({
      body: JSON.stringify({
        ...input,
        snapshots: [{ ...input.snapshots[0], zoneId: null }]
      }),
      method: "POST"
    }));
  });

  it("derives Beaufort from wind speed before saving simulation datasets", async () => {
    const input: CreateSimulationDatasetInput = {
      description: "Tinh Beaufort tu gio",
      name: "Kich ban gio",
      portCode: "DNTSA",
      snapshots: [{
        beaufortNumber: 0,
        rainfall1hMm: 28,
        snapshotNumber: 1,
        visibilityKm: 4,
        windSpeedMs: 18,
        zoneId: "zone-1"
      }]
    };
    const dataset = { datasetId: "dataset-beaufort", description: input.description, name: input.name, portCode: "DNTSA", snapshotCount: 1 };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(dataset), { status: 201 }));

    await expect(createSimulationDataset(input)).resolves.toEqual(dataset);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/simulation/datasets", expect.objectContaining({
      body: JSON.stringify({
        ...input,
        snapshots: [{ ...input.snapshots[0], beaufortNumber: 8 }]
      }),
      method: "POST"
    }));
  });

  it("updates and deletes simulation datasets through the real API", async () => {
    const input: CreateSimulationDatasetInput = {
      description: "Kich ban da sua",
      name: "Bao test updated",
      portCode: "DNTSA",
      snapshots: [{
        beaufortNumber: 7,
        rainfall1hMm: 18,
        snapshotNumber: 1,
        visibilityKm: 6,
        windSpeedMs: 15,
        zoneId: ""
      }]
    };
    const dataset = { datasetId: "dataset-1", description: input.description, name: input.name, portCode: "DNTSA", snapshotCount: 1 };
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify(dataset), { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(updateSimulationDataset("dataset-1", input)).resolves.toEqual(dataset);
    await expect(deleteSimulationDataset("dataset-1")).resolves.toBeUndefined();

    expect(fetch).toHaveBeenNthCalledWith(1, "http://localhost:5000/api/simulation/datasets/dataset-1", expect.objectContaining({
      body: JSON.stringify({
        ...input,
        snapshots: [{ ...input.snapshots[0], zoneId: null }]
      }),
      method: "PUT"
    }));
    expect(fetch).toHaveBeenNthCalledWith(2, "http://localhost:5000/api/simulation/datasets/dataset-1", expect.objectContaining({
      method: "DELETE"
    }));
  });

  it("loads a simulation dataset detail for editing", async () => {
    const detail = {
      datasetId: "dataset-1",
      description: "Kich ban",
      name: "Bao test",
      portCode: "DNTSA",
      snapshotCount: 1,
      snapshots: [{
        beaufortNumber: 8,
        rainfall1hMm: 28,
        snapshotNumber: 1,
        visibilityKm: 4,
        windSpeedMs: 18,
        zoneId: "zone-1"
      }]
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(detail), { status: 200 }));

    await expect(getSimulationDataset("dataset-1")).resolves.toEqual(detail);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/simulation/datasets/dataset-1",
      expect.any(Object)
    );
  });

  it("loads simulation result with dangerous zones and generated tasks", async () => {
    const result = {
      dangerousZones: [{ riskLevel: "CRITICAL", zoneId: "zone-1", zoneName: "Ben so 1" }],
      mapPoints: [{ latitude: 16.1, longitude: 108.2, riskLevel: "CRITICAL", zoneId: "zone-1", zoneName: "Ben so 1" }],
      sessionId: "session-1",
      tasks: [{ priority: "HIGH", taskCode: "TASK-1", title: "Dung boc xep", zoneName: "Ben so 1" }]
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(result), { status: 200 }));

    await expect(getSimulationResult("session-1")).resolves.toEqual(result);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/simulation/session-1/result",
      expect.any(Object)
    );
  });

  it("loads seed map points for the simulation map", async () => {
    const points = [
      { latitude: 16.1, longitude: 108.2, riskLevel: "LOW", zoneId: "zone-1", zoneName: "Ben so 1" }
    ];
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(points), { status: 200 }));

    await expect(getSimulationMapPoints()).resolves.toEqual(points);

    expect(fetch).toHaveBeenCalledWith(
      "http://localhost:5000/api/simulation/map-points",
      expect.any(Object)
    );
  });

  it("creates a future forecast plan for simulation planning", async () => {
    const plan = {
      dataset: { datasetId: "forecast-1", description: "Du bao 5 ngay", name: "Ke hoach du bao", portCode: "DNTSA", snapshotCount: 5 },
      generatedAt: "2026-07-02T10:00:00Z",
      horizonDays: 5,
      items: [{
        operationPlan: "Van hanh binh thuong",
        plannedAt: "2026-07-03T00:00:00Z",
        rainRiskLevel: "LOW",
        riskLevel: "LOW",
        summary: "Du bao on dinh",
        visibilityRiskLevel: "LOW",
        windRiskLevel: "LOW"
      }],
      sourceObservedAt: "2026-07-02T09:00:00Z"
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(plan), { status: 200 }));

    await expect(createForecastPlan({ horizonDays: 5, portCode: "DNTSA" })).resolves.toEqual(plan);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/simulation/forecast-plan", expect.objectContaining({
      body: JSON.stringify({ horizonDays: 5, portCode: "DNTSA" }),
      method: "POST"
    }));
  });
});
