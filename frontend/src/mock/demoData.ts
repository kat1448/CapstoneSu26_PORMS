import type { AlertItem } from "../types/alert";
import type {
  DashboardSummary,
  OperationMode,
  RiskLevel,
  RiskTrendPoint,
  WeatherSnapshot
} from "../types/dashboard";
import type { OperationEvent } from "../types/log";
import type { PortSummary, PortZone } from "../types/port";
import type { SimulationSnapshot } from "../types/simulation";

type UsersPageRecord = {
  email: string;
  fullName: string;
  lastLoginLabel: string;
  portId?: string | null;
  portName: string;
  role: string;
  status: "ACTIVE" | "INACTIVE" | "LOCKED";
  userId: string;
};

type DemoState = {
  alerts: AlertItem[];
  dashboardSummary: DashboardSummary;
  operationEvents: OperationEvent[];
  ports: PortSummary[];
  riskTrend: RiskTrendPoint[];
  simulation: SimulationSnapshot;
  users: UsersPageRecord[];
  weather: WeatherSnapshot;
  zonesByPortId: Record<string, PortZone[]>;
};

const primaryPortId = "port-dntsa";

const baseState: DemoState = {
  dashboardSummary: {
    activeAlertCount: 3,
    beaufortNumber: 8,
    currentOperationMode: "LIMITED",
    currentRiskLevel: "HIGH",
    portCode: "DNTSA",
    portId: primaryPortId,
    portName: "Cảng Tiên Sa",
    rainfall1hMm: 28.5,
    visibilityKm: 4.2,
    windSpeedMs: 18.4
  },
  weather: {
    beaufortNumber: 8,
    dataPoints: [{
      beaufortNumber: 8,
      dataSource: "OPENWEATHER_API",
      latitude: 16.116235,
      longitude: 108.230378,
      observedAt: "2026-06-19T14:28:23Z",
      portCode: "DNTSA",
      portName: "Cảng Tiên Sa",
      rainfall1hMm: 28.5,
      temperatureC: 27,
      visibilityKm: 4.2,
      weatherDescription: "moderate rain",
      windSpeedMs: 18.4,
      zoneName: "Toàn cảng"
    }],
    dataSource: "OPENWEATHER_API",
    humidityPct: 91,
    observedAt: "2026-06-19T14:28:23Z",
    pressureHpa: 1008,
    rainfall1hMm: 28.5,
    recordedAt: "2026-06-19T14:29:12Z",
    temperatureC: 27,
    visibilityKm: 4.2,
    weatherCode: 500,
    weatherDescription: "moderate rain",
    windDirectionDeg: 110,
    windGustMs: 22.4,
    windSpeedMs: 18.4
  },
  riskTrend: [
    { hourLabel: "00:00", riskScore: 1 },
    { hourLabel: "03:00", riskScore: 1 },
    { hourLabel: "06:00", riskScore: 2 },
    { hourLabel: "09:00", riskScore: 2 },
    { hourLabel: "12:00", riskScore: 3 },
    { hourLabel: "15:00", riskScore: 2 },
    { hourLabel: "18:00", riskScore: 3 },
    { hourLabel: "21:00", riskScore: 4 }
  ],
  ports: [
    {
      activeAlertCount: 3,
      currentOperationMode: "LIMITED",
      currentRiskLevel: "HIGH",
      isActive: true,
      latitude: 16.1228,
      longitude: 108.2144,
      portCode: "DNTSA",
      portId: primaryPortId,
      portName: "Cảng Tiên Sa",
      updatedAtLabel: "Vừa cập nhật"
    },
    {
      activeAlertCount: 1,
      currentOperationMode: "NORMAL",
      currentRiskLevel: "MEDIUM",
      isActive: true,
      latitude: 16.1650,
      longitude: 108.1915,
      portCode: "DNLH",
      portId: "port-lien-chieu",
      portName: "Cảng Liên Chiểu",
      updatedAtLabel: "5 phút trước"
    },
    {
      activeAlertCount: 0,
      currentOperationMode: "NORMAL",
      currentRiskLevel: "LOW",
      isActive: true,
      latitude: 16.3378,
      longitude: 108.0144,
      portCode: "DNCM",
      portId: "port-chan-may",
      portName: "Cảng Chân Mây",
      updatedAtLabel: "12 phút trước"
    }
  ],
  zonesByPortId: {
    [primaryPortId]: [
      {
        capacityLabel: "2 tàu",
        currentRiskLevel: "CRITICAL",
        displayOrder: 1,
        isActive: true,
        isRestricted: true,
        latitude: 16.1240,
        longitude: 108.2140,
        overrideEnabled: true,
        portId: primaryPortId,
        restrictionReason: "Dừng bốc xếp do gió cấp 10",
        statusLabel: "Tạm dừng",
        zoneId: "zone-b1",
        zoneName: "Bến số 1",
        zoneType: "DOCK"
      },
      {
        capacityLabel: "2 tàu",
        currentRiskLevel: "HIGH",
        displayOrder: 2,
        isActive: true,
        isRestricted: true,
        latitude: 16.1245,
        longitude: 108.2145,
        overrideEnabled: false,
        portId: primaryPortId,
        restrictionReason: "Hạn chế thiết bị nâng cao",
        statusLabel: "Hạn chế",
        zoneId: "zone-b2",
        zoneName: "Bến số 2",
        zoneType: "DOCK"
      },
      {
        capacityLabel: "1200 TEU",
        currentRiskLevel: "HIGH",
        displayOrder: 3,
        isActive: true,
        isRestricted: false,
        latitude: 16.1230,
        longitude: 108.2160,
        overrideEnabled: false,
        portId: primaryPortId,
        restrictionReason: null,
        statusLabel: "Tăng giám sát",
        zoneId: "zone-yard-a",
        zoneName: "Bãi container A",
        zoneType: "YARD"
      },
      {
        capacityLabel: "8 làn",
        currentRiskLevel: "MEDIUM",
        displayOrder: 4,
        isActive: true,
        isRestricted: false,
        latitude: 16.1250,
        longitude: 108.2130,
        overrideEnabled: false,
        portId: primaryPortId,
        restrictionReason: null,
        statusLabel: "Giảm tốc độ",
        zoneId: "zone-gate-main",
        zoneName: "Cổng chính",
        zoneType: "GATE"
      }
    ],
    "port-lien-chieu": [
      {
        capacityLabel: "4 tàu",
        currentRiskLevel: "MEDIUM",
        displayOrder: 1,
        isActive: true,
        isRestricted: false,
        latitude: 16.1650,
        longitude: 108.1915,
        overrideEnabled: false,
        portId: "port-lien-chieu",
        restrictionReason: null,
        statusLabel: "Ổn định",
        zoneId: "zone-lc-dock",
        zoneName: "Cầu cảng số 1",
        zoneType: "DOCK"
      }
    ],
    "port-chan-may": [
      {
        capacityLabel: "3000 m2",
        currentRiskLevel: "LOW",
        displayOrder: 1,
        isActive: true,
        isRestricted: false,
        latitude: 16.3378,
        longitude: 108.0144,
        overrideEnabled: false,
        portId: "port-chan-may",
        restrictionReason: null,
        statusLabel: "Bình thường",
        zoneId: "zone-cm-warehouse",
        zoneName: "Kho tổng hợp",
        zoneType: "WAREHOUSE"
      }
    ]
  },
  alerts: [
    {
      alertId: "alert-1",
      alertType: "RISK_CHANGED",
      createdAt: "2026-06-19T14:28:23",
      message: "Gió cấp 10 và mưa lớn kích hoạt quy trình dừng bốc xếp.",
      portCode: "DNTSA",
      portId: primaryPortId,
      portName: "Cảng Tiên Sa",
      read: false,
      severity: "CRITICAL",
      title: "Rủi ro đạt CRITICAL",
      zoneName: "Bến số 1"
    },
    {
      alertId: "alert-2",
      alertType: "MODE_CHANGED",
      createdAt: "2026-06-19T14:22:23",
      message: "Hệ thống chuyển sang LIMITED theo SOP-HIGH-DOCK-01.",
      portCode: "DNTSA",
      portId: primaryPortId,
      portName: "Cảng Tiên Sa",
      read: false,
      severity: "HIGH",
      title: "Chế độ vận hành chuyển sang LIMITED",
      zoneName: "Toàn cảng"
    },
    {
      alertId: "alert-3",
      alertType: "WEATHER",
      createdAt: "2026-06-19T14:14:23",
      message: "Lượng mưa đã vượt ngưỡng HIGH 25 mm/h.",
      portCode: "DNTSA",
      portId: primaryPortId,
      portName: "Cảng Tiên Sa",
      read: false,
      severity: "HIGH",
      title: "Lượng mưa vượt ngưỡng cảnh báo",
      zoneName: "Cảng Tiên Sa"
    },
    {
      alertId: "alert-4",
      alertType: "SYSTEM",
      createdAt: "2026-06-19T13:58:23",
      message: "Collector đã đồng bộ 6 khu vực thời tiết thành công.",
      portCode: "DNTSA",
      portId: primaryPortId,
      portName: "Cảng Tiên Sa",
      read: true,
      severity: "LOW",
      title: "Đồng bộ dữ liệu thời tiết thành công",
      zoneName: "Toàn cảng"
    }
  ],
  operationEvents: [
    {
      actorName: "SYSTEM",
      entityType: "risk_assessment",
      eventType: "RISK_CHANGED",
      occurredAt: "2026-06-19T21:54:12",
      operationEventId: "event-1",
      portCode: "DNTSA",
      summary: "Mức rủi ro thay đổi LOW sang HIGH sau đánh giá gió cấp 8.",
      tone: "warning"
    },
    {
      actorName: "SYSTEM",
      entityType: "sop_execution",
      eventType: "SOP_EXECUTED",
      occurredAt: "2026-06-19T21:54:13",
      operationEventId: "event-2",
      portCode: "DNTSA",
      summary: "Thực thi SOP-HIGH-DOCK-01 và tạo nhiệm vụ hạn chế bốc xếp.",
      tone: "info"
    },
    {
      actorName: "SYSTEM",
      entityType: "operation_mode",
      eventType: "MODE_CHANGED",
      occurredAt: "2026-06-19T21:54:14",
      operationEventId: "event-3",
      portCode: "DNTSA",
      summary: "Chế độ vận hành chuyển từ NORMAL sang LIMITED.",
      tone: "danger"
    },
    {
      actorName: "Nguyễn Văn Hùng",
      entityType: "user_session",
      eventType: "USER_LOGIN",
      occurredAt: "2026-06-19T21:48:02",
      operationEventId: "event-4",
      portCode: "DNTSA",
      summary: "Đăng nhập thành công từ trình duyệt Chrome.",
      tone: "success"
    }
  ],
  simulation: {
    beaufortNumber: 8,
    currentMode: "LIMITED",
    currentRiskLevel: "HIGH",
    feed: [],
    generatedAlertCount: 0,
    modeChangeCount: 0,
    rainfall1hMm: 28.5,
    progressPercent: 0,
    status: "IDLE",
    visibilityKm: 4.2,
    windSpeedMs: 18.4
  },
  users: [
    {
      email: "hung@example.com",
      fullName: "Nguyễn Văn Hùng",
      lastLoginLabel: "Vừa xong",
      portName: "Tiên Sa",
      role: "PORT_OPERATOR",
      status: "ACTIVE",
      userId: "user-1"
    },
    {
      email: "lan@example.com",
      fullName: "Trần Thị Lan",
      lastLoginLabel: "15:30 hôm nay",
      portName: "Tất cả",
      role: "COMPANY_ADMIN",
      status: "ACTIVE",
      userId: "user-2"
    },
    {
      email: "duc@example.com",
      fullName: "Phạm Minh Đức",
      lastLoginLabel: "13:20 hôm nay",
      portName: "Tiên Sa",
      role: "PORT_OPERATOR",
      status: "ACTIVE",
      userId: "user-3"
    }
  ]
};

let demoState: DemoState = clone(baseState);
let listeners = new Set<() => void>();

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function notify() {
  listeners.forEach((listener) => listener());
}

function badgeStatusFromRisk(riskLevel: RiskLevel): string {
  switch (riskLevel) {
    case "LOW":
      return "Bình thường";
    case "MEDIUM":
      return "Tăng giám sát";
    case "HIGH":
      return "Hạn chế";
    case "CRITICAL":
      return "Tạm dừng";
  }
}

function setPortRiskAndMode(riskLevel: RiskLevel, operationMode: OperationMode) {
  demoState.dashboardSummary.currentRiskLevel = riskLevel;
  demoState.dashboardSummary.currentOperationMode = operationMode;
  demoState.ports = demoState.ports.map((port) =>
    port.portId === primaryPortId
      ? {
          ...port,
          activeAlertCount: demoState.dashboardSummary.activeAlertCount,
          currentOperationMode: operationMode,
          currentRiskLevel: riskLevel,
          updatedAtLabel: "Vua cap nhat"
        }
      : port,
  );
  demoState.zonesByPortId[primaryPortId] = demoState.zonesByPortId[primaryPortId].map((zone, index) => {
    if (index === 0) {
      return {
        ...zone,
        currentRiskLevel: riskLevel,
        isRestricted: riskLevel !== "LOW",
        restrictionReason: riskLevel === "CRITICAL" ? "Dung boc xep do gio cap 10" : zone.restrictionReason,
        statusLabel: badgeStatusFromRisk(riskLevel)
      };
    }

    if (index === 1) {
      return {
        ...zone,
        currentRiskLevel: riskLevel === "CRITICAL" ? "HIGH" : riskLevel,
        statusLabel: badgeStatusFromRisk(riskLevel === "CRITICAL" ? "HIGH" : riskLevel)
      };
    }

    return zone;
  });
}

export function subscribeToDemoData(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function getDashboardSummary(): DashboardSummary {
  return clone(demoState.dashboardSummary);
}

export function getWeatherSnapshot(): WeatherSnapshot {
  return clone(demoState.weather);
}

export function getRiskTrend(): RiskTrendPoint[] {
  return clone(demoState.riskTrend);
}

export function getAlerts(): AlertItem[] {
  return clone(demoState.alerts);
}

export function getOperationEvents(): OperationEvent[] {
  return clone(demoState.operationEvents);
}

export function getPorts(): PortSummary[] {
  return clone(demoState.ports);
}

export function getPortZones(portId: string): PortZone[] {
  return clone(demoState.zonesByPortId[portId] ?? []);
}

export function updatePort(portId: string, input: DemoPortUpdateInput): PortSummary {
  let updated: PortSummary | null = null;
  demoState.ports = demoState.ports.map((port) => {
    if (port.portId !== portId) {
      return port;
    }

    updated = {
      ...port,
      isActive: input.isActive,
      latitude: input.latitude,
      longitude: input.longitude,
      portCode: input.code,
      portName: input.name
    };
    return updated;
  });

  notify();
  return clone(updated ?? demoState.ports.find((port) => port.portId === portId)!);
}

export function getSimulationSnapshot(): SimulationSnapshot {
  return clone(demoState.simulation);
}

export function getUsers(): UsersPageRecord[] {
  return clone(demoState.users);
}

export type DemoUserCreateInput = {
  email: string;
  fullName: string;
  password: string;
  portId?: string | null;
  role: string;
  status: "ACTIVE" | "INACTIVE" | "LOCKED";
};

export type DemoUserUpdateInput = Omit<DemoUserCreateInput, "password">;

export type DemoPortUpdateInput = {
  address?: string | null;
  code: string;
  isActive: boolean;
  latitude: number;
  longitude: number;
  name: string;
  timezone: string;
  weatherSource: string;
  weatherStationId?: string | null;
};

function portNameFromId(portId: string | null | undefined): string {
  if (!portId) {
    return "Tất cả";
  }

  return demoState.ports.find((port) => port.portId === portId)?.portName ?? "Tất cả";
}

export function createUser(input: DemoUserCreateInput): UsersPageRecord {
  const user: UsersPageRecord = {
    email: input.email,
    fullName: input.fullName,
    lastLoginLabel: "Chưa đăng nhập",
    portId: input.portId ?? null,
    portName: portNameFromId(input.portId),
    role: input.role,
    status: input.status,
    userId: `user-${Date.now()}`
  };

  demoState.users = [user, ...demoState.users];
  notify();
  return clone(user);
}

export function updateUser(userId: string, input: DemoUserUpdateInput): UsersPageRecord {
  let updated: UsersPageRecord | null = null;
  demoState.users = demoState.users.map((user) => {
    if (user.userId !== userId) {
      return user;
    }

    updated = {
      ...user,
      email: input.email,
      fullName: input.fullName,
      portId: input.portId ?? null,
      portName: portNameFromId(input.portId),
      role: input.role,
      status: input.status
    };
    return updated;
  });

  notify();
  return clone(updated ?? demoState.users.find((user) => user.userId === userId)!);
}

export function deleteUser(userId: string): void {
  demoState.users = demoState.users.filter((user) => user.userId !== userId);
  notify();
}

export function resetDemoData() {
  demoState = clone(baseState);
  notify();
}

export async function runDemoStepSequence(): Promise<void> {
  const nowLabel = new Date().toISOString();
  const steps: Array<{
    beaufort: number;
    detail: string;
    mode: OperationMode;
    progressPercent: number;
    rain: number;
    risk: RiskLevel;
    title: string;
    visibility: number;
    wind: number;
  }> = [
    {
      detail: "Tăng tần suất giám sát và giảm tốc phương tiện.",
      beaufort: 6,
      mode: "NORMAL",
      progressPercent: 25,
      rain: 12.5,
      risk: "MEDIUM",
      title: "Rủi ro tăng lên MEDIUM",
      visibility: 8,
      wind: 10.8
    },
    {
      detail: "Hạn chế bốc xếp và tạo nhiệm vụ vận hành.",
      beaufort: 8,
      mode: "LIMITED",
      progressPercent: 55,
      rain: 28.5,
      risk: "HIGH",
      title: "Kích hoạt SOP mức HIGH",
      visibility: 4.2,
      wind: 18.4
    },
    {
      detail: "Dừng toàn bộ hoạt động cầu cảng trong kịch bản demo.",
      beaufort: 10,
      mode: "STOP",
      progressPercent: 100,
      rain: 54.8,
      risk: "CRITICAL",
      title: "Rủi ro đạt CRITICAL",
      visibility: 0.8,
      wind: 25.2
    }
  ];

  demoState.simulation = {
    currentMode: demoState.dashboardSummary.currentOperationMode,
    currentRiskLevel: demoState.dashboardSummary.currentRiskLevel,
    feed: [],
    beaufortNumber: demoState.dashboardSummary.beaufortNumber ?? 8,
    generatedAlertCount: 0,
    modeChangeCount: 0,
    rainfall1hMm: demoState.weather.rainfall1hMm,
    progressPercent: 0,
    status: "RUNNING",
    visibilityKm: demoState.weather.visibilityKm,
    windSpeedMs: demoState.weather.windSpeedMs
  };
  notify();

  for (const step of steps) {
    await new Promise((resolve) => setTimeout(resolve, 250));
    const previousMode = demoState.dashboardSummary.currentOperationMode;
    demoState.weather.rainfall1hMm = step.rain;
    demoState.weather.visibilityKm = step.visibility;
    demoState.weather.windSpeedMs = step.wind;
    demoState.dashboardSummary.rainfall1hMm = step.rain;
    demoState.dashboardSummary.visibilityKm = step.visibility;
    demoState.dashboardSummary.windSpeedMs = step.wind;
    demoState.dashboardSummary.beaufortNumber = step.beaufort;

    if (step.risk === "HIGH" || step.risk === "CRITICAL") {
      demoState.dashboardSummary.activeAlertCount += 1;
      demoState.simulation.generatedAlertCount += 1;
      demoState.alerts.unshift({
        alertId: `alert-sim-${step.risk.toLowerCase()}`,
        alertType: "SIMULATION",
        createdAt: nowLabel,
        message: step.detail,
        portCode: "DNTSA",
        portId: primaryPortId,
        portName: "Cảng Tiên Sa",
        read: false,
        severity: step.risk,
        title: `[Mô phỏng] ${step.title}`,
        zoneName: "Toàn cảng"
      });
    }

    if (step.mode !== previousMode) {
      demoState.simulation.modeChangeCount += 1;
    }

    setPortRiskAndMode(step.risk, step.mode);

    demoState.simulation.currentMode = step.mode;
    demoState.simulation.currentRiskLevel = step.risk;
    demoState.simulation.beaufortNumber = step.beaufort;
    demoState.simulation.rainfall1hMm = step.rain;
    demoState.simulation.progressPercent = step.progressPercent;
    demoState.simulation.visibilityKm = step.visibility;
    demoState.simulation.windSpeedMs = step.wind;
    demoState.simulation.feed.unshift({
      detail: step.detail,
      happenedAt: nowLabel,
      riskLevel: step.risk,
      title: step.title
    });

    demoState.operationEvents.unshift({
      actorName: "SYSTEM",
      entityType: "simulation_session",
      eventType: "SIMULATION_STEP",
      occurredAt: nowLabel,
      operationEventId: `event-sim-${step.progressPercent}`,
      portCode: "DNTSA",
      summary: `${step.title}. Chế độ hiện tại ${step.mode}.`,
      tone: step.risk === "CRITICAL" ? "danger" : step.risk === "HIGH" ? "warning" : "info"
    });

    notify();
  }

  demoState.simulation.status = "COMPLETED";
  notify();
}
