import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { RiskTrendChart } from "./RiskTrendChart";

describe("RiskTrendChart", () => {
  it("shows an empty state when there is no real trend data", () => {
    render(<RiskTrendChart currentRiskLevel="LOW" points={[]} />);

    expect(screen.getByText("Chưa có dữ liệu rủi ro 24 giờ")).toBeInTheDocument();
  });

  it("renders hover targets and tooltip labels for real trend points", () => {
    render(
      <RiskTrendChart
        currentRiskLevel="HIGH"
        points={[
          { hourLabel: "08:00", riskScore: 1 },
          { hourLabel: "09:00", riskScore: 3 },
          { hourLabel: "10:00", riskScore: 4 }
        ]}
      />
    );

    expect(screen.getByTestId("risk-trend-tooltip-layer")).toBeInTheDocument();
    expect(screen.getByLabelText("08:00 LOW score 1")).toBeInTheDocument();
    expect(screen.getByLabelText("09:00 HIGH score 3")).toBeInTheDocument();
    expect(screen.getByLabelText("10:00 CRITICAL score 4")).toBeInTheDocument();
  });
});
