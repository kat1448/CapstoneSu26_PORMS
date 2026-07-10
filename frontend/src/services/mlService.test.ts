import { afterEach, describe, expect, it, vi } from "vitest";
import { analyzeForecastRisk } from "./mlService";

vi.stubGlobal("fetch", vi.fn());

afterEach(() => {
  vi.clearAllMocks();
});

describe("mlService", () => {
  it("posts forecast items to the PCA K-Means risk endpoint", async () => {
    const response = {
      items: [{
        clusterId: 3,
        clusterLabel: "SEVERE_OPERATION_RISK",
        dominantFactors: ["RAIN", "VISIBILITY"],
        mlRecommendation: "STOP",
        pcaRiskScore: 92,
        plannedAt: "2026-07-14T00:00:00Z",
        ruleRiskLevel: "CRITICAL"
      }],
      llmPlanAnalysis: {
        isConfigured: true,
        items: [{
          affectedOperations: ["Bốc xếp"],
          operationMode: "STOP",
          planChange: "Dừng bốc xếp khu vực nguy hiểm.",
          plannedAt: "2026-07-14T00:00:00Z",
          reason: "CRITICAL với mưa lớn và tầm nhìn thấp.",
          recommendedActions: ["Dừng bốc xếp", "Thông báo đội vận hành"]
        }],
        model: "gemini-1.5-flash",
        portCode: "DNTSA",
        provider: "GEMINI",
        summary: "Rủi ro tăng, kế hoạch chuyển sang STOP."
      },
      modelVersion: "pca-kmeans-v1",
      portCode: "DNTSA"
    };
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify(response), { status: 200 }));

    await expect(analyzeForecastRisk({
      items: [{
        plannedAt: "2026-07-14T00:00:00Z",
        rainRiskLevel: "CRITICAL",
        ruleRiskLevel: "CRITICAL",
        visibilityRiskLevel: "CRITICAL",
        windRiskLevel: "HIGH"
      }],
      portCode: "DNTSA"
    })).resolves.toEqual(response);

    expect(fetch).toHaveBeenCalledWith("http://localhost:5000/api/ml/forecast-risk-analysis", expect.objectContaining({
      body: JSON.stringify({
        items: [{
          plannedAt: "2026-07-14T00:00:00Z",
          rainRiskLevel: "CRITICAL",
          ruleRiskLevel: "CRITICAL",
          visibilityRiskLevel: "CRITICAL",
          windRiskLevel: "HIGH"
        }],
        portCode: "DNTSA"
      }),
      method: "POST"
    }));
  });
});
