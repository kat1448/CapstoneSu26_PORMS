import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { DemoUser } from "../../App";
import { acknowledgeAlert, getAlerts, getAlertSpeechAudio } from "../../services/alertService";
import type { AlertItem } from "../../types/alert";
import { AppShell } from "./AppShell";

vi.mock("../../services/alertService", () => ({
  acknowledgeAlert: vi.fn(),
  getAlertSpeechAudio: vi.fn(),
  getAlertSpeechUrl: vi.fn((alertId: string) => `http://localhost:5000/api/alerts/${alertId}/speech`),
  getAlerts: vi.fn()
}));

const currentUser: DemoUser = {
  email: "admin@porms.vn",
  initials: "NV",
  name: "Nguyen Van Hung",
  portName: "Cang Tien Sa",
  role: "ADMIN" as const
};

const portManagerUser: DemoUser = {
  email: "manager@porms.vn",
  initials: "QL",
  name: "Port Manager",
  portId: "port-1",
  portName: "Cang Tien Sa",
  role: "PORT_MANAGER"
};

const operatorUser: DemoUser = {
  email: "operator@porms.vn",
  initials: "NV",
  name: "Operator",
  portId: "port-1",
  portName: "Cang Tien Sa",
  role: "OPERATOR"
};

const unreadAlert: AlertItem = {
  alertId: "alert-1",
  alertType: "WEATHER",
  createdAt: "17/07/2026 09:00",
  createdAtIso: "2026-07-17T02:00:00Z",
  expiresAt: "2099-01-01T00:00:00Z",
  message: "Gio manh tai ben so 1.",
  portCode: "DNTSA",
  portId: "port-1",
  portName: "Cang Tien Sa",
  read: false,
  severity: "HIGH" as const,
  title: "Canh bao gio manh",
  zoneName: "Ben so 1"
};

function renderShell(user: DemoUser = currentUser) {
  return render(
    <MemoryRouter initialEntries={["/dashboard"]}>
      <AppShell
        currentUser={user}
        onLogout={() => undefined}
        onRefresh={() => undefined}
        refreshKey={0}
      >
        <div>Content</div>
      </AppShell>
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.mocked(acknowledgeAlert).mockResolvedValue();
  vi.mocked(getAlertSpeechAudio).mockResolvedValue(new Blob(["audio"], { type: "audio/mpeg" }));
  vi.mocked(getAlerts).mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("AppShell", () => {
  it("opens and dismisses the mobile sidebar", async () => {
    const user = userEvent.setup();
    renderShell();

    await user.click(screen.getByRole("button", { name: /menu/i }));
    expect(screen.getByRole("navigation")).toHaveClass("open");

    await user.click(screen.getByTestId("sidebar-backdrop"));
    expect(screen.getByRole("navigation")).not.toHaveClass("open");
  });

  it("shows a popup and plays a sound for unread alerts", async () => {
    const user = userEvent.setup();
    const oscillator = { connect: vi.fn(), frequency: { value: 0 }, start: vi.fn(), stop: vi.fn(), type: "" };
    const gain = { connect: vi.fn(), gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() } };
    const audioContext = {
      close: vi.fn(),
      createGain: vi.fn(() => gain),
      createOscillator: vi.fn(() => oscillator),
      currentTime: 0,
      destination: {}
    };
    vi.stubGlobal("AudioContext", vi.fn(() => audioContext));
    vi.mocked(getAlerts).mockResolvedValue([unreadAlert]);

    renderShell();

    expect(await screen.findByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
    expect(screen.getByText("Canh bao gio manh")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Bật âm thanh" })).not.toBeInTheDocument();
    await waitFor(() => expect(oscillator.start).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: "Xác nhận" }));

    expect(screen.queryByRole("dialog", { name: "Cảnh báo mới" })).not.toBeInTheDocument();
  });

  it.each(["HIGH", "CRITICAL"] as const)(
    "shows an active unread %s alert returned for an Operator",
    async (severity) => {
      vi.mocked(getAlerts).mockResolvedValue([{ ...unreadAlert, severity }]);

      renderShell(operatorUser);

      expect(await screen.findByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
      expect(screen.getByText("Canh bao gio manh")).toBeInTheDocument();
    }
  );

  it.each([
    ["Port Manager", portManagerUser],
    ["Operator", operatorUser]
  ] as const)("does not notify %s about an expired alert", async (_role, user) => {
    vi.mocked(getAlerts).mockResolvedValue([{
      ...unreadAlert,
      expiresAt: "2000-01-01T00:00:00Z"
    }]);

    renderShell(user);

    await waitFor(() => expect(getAlerts).toHaveBeenCalled());
    expect(screen.queryByRole("dialog", { name: "Cảnh báo mới" })).not.toBeInTheDocument();
    expect(screen.getByLabelText("0 cảnh báo chưa đọc")).toBeInTheDocument();
    expect(getAlertSpeechAudio).not.toHaveBeenCalled();
  });

  it("skips an expired alert and shows the next active alert", async () => {
    vi.mocked(getAlerts).mockResolvedValue([
      { ...unreadAlert, expiresAt: "2000-01-01T00:00:00Z", title: "Old alert" },
      { ...unreadAlert, alertId: "alert-2", title: "Active alert" }
    ]);

    renderShell(portManagerUser);

    expect(await screen.findByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
    expect(screen.getByText("Active alert")).toBeInTheDocument();
    expect(screen.queryByText("Old alert")).not.toBeInTheDocument();
    expect(screen.getAllByLabelText("1 cảnh báo chưa đọc")).toHaveLength(2);
  });

  it("does not notify about a legacy alert older than two hours", async () => {
    vi.mocked(getAlerts).mockResolvedValue([{
      ...unreadAlert,
      createdAtIso: "2000-01-01T00:00:00Z",
      expiresAt: null
    }]);

    renderShell(portManagerUser);

    await waitFor(() => expect(getAlerts).toHaveBeenCalled());
    expect(screen.queryByRole("dialog", { name: "Cảnh báo mới" })).not.toBeInTheDocument();
    expect(screen.getByLabelText("0 cảnh báo chưa đọc")).toBeInTheDocument();
  });

  it.each(["LOW", "MEDIUM"] as const)(
    "does not show a popup or play a sound for %s alerts",
    async (severity) => {
      const oscillator = { connect: vi.fn(), frequency: { value: 0 }, start: vi.fn(), stop: vi.fn(), type: "" };
      const gain = { connect: vi.fn(), gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() } };
      const audioContext = {
        createGain: vi.fn(() => gain),
        createOscillator: vi.fn(() => oscillator),
        currentTime: 0,
        destination: {}
      };
      vi.stubGlobal("AudioContext", vi.fn(() => audioContext));
      vi.mocked(getAlerts).mockResolvedValue([{ ...unreadAlert, alertId: `alert-${severity}`, severity }]);

      renderShell();

      await waitFor(() => expect(getAlerts).toHaveBeenCalled());
      expect(screen.queryByRole("dialog", { name: "Cảnh báo mới" })).not.toBeInTheDocument();
      expect(oscillator.start).not.toHaveBeenCalled();
    }
  );

  it("reads a high alert through the PORMS speech endpoint, supports replay, and stops after acknowledgement", async () => {
    vi.useFakeTimers();
    const audioInstances: MockAudio[] = [];

    class MockAudio {
      load = vi.fn();
      onended: (() => void) | null = null;
      onerror: (() => void) | null = null;
      onplaying: (() => void) | null = null;
      pause = vi.fn();
      preload = "";
      removeAttribute = vi.fn();
      src: string;

      constructor(src: string) {
        this.src = src;
        audioInstances.push(this);
      }

      play = vi.fn(() => {
        this.onplaying?.();
        return Promise.resolve();
      });
    }

    vi.stubGlobal("Audio", MockAudio);
    vi.stubGlobal("URL", {
      ...URL,
      createObjectURL: vi.fn(() => "blob:porms-alert-speech"),
      revokeObjectURL: vi.fn()
    });
    vi.mocked(getAlerts).mockResolvedValue([unreadAlert]);

    renderShell();
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });

    expect(screen.getByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
    act(() => vi.advanceTimersByTime(900));

    await act(async () => { await Promise.resolve(); await Promise.resolve(); });
    expect(getAlertSpeechAudio).toHaveBeenCalledWith("alert-1");
    expect(audioInstances).toHaveLength(1);
    expect(audioInstances[0].src).toBe("blob:porms-alert-speech");
    expect(screen.queryByText(/Đang đọc cảnh báo/)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Nghe cảnh báo/ }));
    await act(async () => { await Promise.resolve(); await Promise.resolve(); });
    expect(audioInstances).toHaveLength(2);

    fireEvent.click(screen.getByRole("button", { name: "Xác nhận" }));
    expect(audioInstances[1].pause).toHaveBeenCalled();
  });

  it("resumes a suspended audio context before playing the alert sound", async () => {
    const oscillator = { connect: vi.fn(), frequency: { value: 0 }, start: vi.fn(), stop: vi.fn(), type: "" };
    const gain = { connect: vi.fn(), gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() } };
    const audioContext = {
      close: vi.fn(),
      createGain: vi.fn(() => gain),
      createOscillator: vi.fn(() => oscillator),
      currentTime: 0,
      destination: {},
      resume: vi.fn().mockResolvedValue(undefined),
      state: "suspended"
    };
    vi.stubGlobal("AudioContext", vi.fn(() => audioContext));
    vi.mocked(getAlerts).mockResolvedValue([unreadAlert]);

    renderShell();

    expect(await screen.findByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
    await waitFor(() => expect(audioContext.resume).toHaveBeenCalled());
  });

  it("does not play a pending alert after the user has acknowledged it", async () => {
    let resolveResume: (() => void) | undefined;
    const oscillator = { connect: vi.fn(), frequency: { value: 0 }, start: vi.fn(), stop: vi.fn(), type: "" };
    const gain = { connect: vi.fn(), gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() } };
    const audioContext = {
      createGain: vi.fn(() => gain),
      createOscillator: vi.fn(() => oscillator),
      currentTime: 0,
      destination: {},
      resume: vi.fn(() => new Promise<void>((resolve) => { resolveResume = resolve; })),
      state: "suspended"
    };
    vi.stubGlobal("AudioContext", vi.fn(() => audioContext));
    vi.mocked(getAlerts).mockResolvedValue([unreadAlert]);

    renderShell();
    expect(await screen.findByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận" }));

    await act(async () => {
      resolveResume?.();
      await Promise.resolve();
    });

    expect(oscillator.start).not.toHaveBeenCalled();
  });

  it("retries alert sound automatically after the next user interaction unlocks audio", async () => {
    const oscillator = { connect: vi.fn(), frequency: { value: 0 }, start: vi.fn(), stop: vi.fn(), type: "" };
    const gain = { connect: vi.fn(), gain: { setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() } };
    const audioContext = {
      close: vi.fn(),
      createGain: vi.fn(() => gain),
      createOscillator: vi.fn(() => oscillator),
      currentTime: 0,
      destination: {},
      resume: vi.fn()
        .mockRejectedValueOnce(new Error("Autoplay blocked"))
        .mockResolvedValue(undefined),
      state: "suspended"
    };
    vi.stubGlobal("AudioContext", vi.fn(() => audioContext));
    vi.mocked(getAlerts).mockResolvedValue([unreadAlert]);

    renderShell();

    expect(await screen.findByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
    await waitFor(() => expect(audioContext.resume).toHaveBeenCalledTimes(1));
    expect(oscillator.start).not.toHaveBeenCalled();

    fireEvent.pointerDown(window);

    await waitFor(() => expect(oscillator.start).toHaveBeenCalled());
  });

  it("snoozes an unread alert for five minutes", async () => {
    vi.useFakeTimers();
    vi.mocked(getAlerts).mockResolvedValue([unreadAlert]);

    renderShell();

    await act(async () => {
      await Promise.resolve();
    });

    expect(screen.getByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Báo sau 5 phút" }));
    expect(screen.queryByRole("dialog", { name: "Cảnh báo mới" })).not.toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(5 * 60 * 1000);
    });

    expect(screen.getByRole("dialog", { name: "Cảnh báo mới" })).toBeInTheDocument();
  });
});
