import type { RiskLevel } from "./dashboard";

export type AlertSeverity = RiskLevel;

export type AlertItem = {
  acknowledged?: boolean;
  acknowledgedAt?: string | null;
  alertId: string;
  alertType: string;
  createdAt: string;
  message: string;
  beaufortNumber?: number | null;
  windSpeedMs?: number | null;
  rainfall1hMm?: number | null;
  visibilityKm?: number | null;
  portCode: string;
  portId: string;
  portName: string;
  read: boolean;
  severity: AlertSeverity;
  status?: "NEW" | "READ" | "ACKNOWLEDGED" | string;
  title: string;
  zoneName: string;
};
