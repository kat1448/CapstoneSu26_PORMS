import type { RiskLevel } from "./dashboard";

export type AlertSeverity = RiskLevel;

export type AlertItem = {
  alertId: string;
  alertType: string;
  createdAt: string;
  message: string;
  portCode: string;
  portId: string;
  portName: string;
  read: boolean;
  severity: AlertSeverity;
  title: string;
  zoneName: string;
};
