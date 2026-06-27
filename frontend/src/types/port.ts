import type { OperationMode, RiskLevel } from "./dashboard";

export type PortSummary = {
  activeAlertCount: number;
  currentOperationMode: OperationMode;
  currentRiskLevel: RiskLevel;
  isActive: boolean;
  latitude?: number | null;
  longitude?: number | null;
  portCode: string;
  portId: string;
  portName: string;
  updatedAtLabel: string;
};

export type PortZone = {
  capacityLabel: string;
  capacityUnit?: string | null;
  capacityValue?: number | null;
  currentRiskLevel: RiskLevel;
  displayOrder: number;
  isActive: boolean;
  isRestricted: boolean;
  latitude?: number | null;
  longitude?: number | null;
  overrideEnabled: boolean;
  portId: string;
  restrictionReason: string | null;
  statusLabel: string;
  zoneId: string;
  zoneName: string;
  zoneType: "DOCK" | "YARD" | "GATE" | "WAREHOUSE";
};
