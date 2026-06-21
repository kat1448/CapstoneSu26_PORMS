import type { OperationMode, RiskLevel } from "./dashboard";

export type PortSummary = {
  activeAlertCount: number;
  currentOperationMode: OperationMode;
  currentRiskLevel: RiskLevel;
  isActive: boolean;
  portCode: string;
  portId: string;
  portName: string;
  updatedAtLabel: string;
};

export type PortZone = {
  capacityLabel: string;
  currentRiskLevel: RiskLevel;
  displayOrder: number;
  isActive: boolean;
  isRestricted: boolean;
  overrideEnabled: boolean;
  portId: string;
  restrictionReason: string | null;
  statusLabel: string;
  zoneId: string;
  zoneName: string;
  zoneType: "DOCK" | "YARD" | "GATE" | "WAREHOUSE";
};
