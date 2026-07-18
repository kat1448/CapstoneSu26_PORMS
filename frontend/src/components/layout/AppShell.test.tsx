import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { acknowledgeAlert, getAlerts } from "../../services/alertService";
import { AppShell } from "./AppShell";

vi.mock("../../services/alertService", () => ({
  acknowledgeAlert: vi.fn(),
  getAlerts: vi.fn()
}));

const currentUser = {
  email: "admin@porms.vn",
  initials: "NV",
  name: "Nguyen Van Hung",
  portName: "Cang Tien Sa",
  role: "ADMIN" as const
};

const unreadAlert = {
  alertId: "alert-1",
  alertType: "WEATHER",
  createdAt: "17/07/2026 09:00",
  message: "Gio manh tai ben so 1.",
  portCode: "DNTSA",
  portId: "port-1",
  portName: "Cang Tien Sa",
  read: false,
  severity: "HIGH" as const,
  title: "Canh bao gio manh",
  zoneName: "Ben so 1"
};

function renderShell() {
  return render(
    <MemoryRouter initialEntries={["/dashboard"]}>
      <AppShell
        currentUser={currentUser}
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
