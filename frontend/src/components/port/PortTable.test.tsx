import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { PortTable } from "./PortTable";

describe("PortTable", () => {
  it("renders port summaries as table rows", () => {
    render(
      <MemoryRouter>
        <PortTable
          ports={[{
            activeAlertCount: 3,
            currentOperationMode: "LIMITED",
            currentRiskLevel: "HIGH",
            isActive: true,
            portCode: "DNTSA",
            portId: "port-1",
            portName: "Cảng Tiên Sa",
            updatedAtLabel: "19/06/2026 14:30:25"
          }]}
        />
      </MemoryRouter>
    );

    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "Mã cảng" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Chi tiết Cảng Tiên Sa" })).toHaveAttribute("href", "/ports/port-1");
  });
});
