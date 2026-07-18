import { useEffect, useMemo, useRef, useState, type PropsWithChildren } from "react";
import type { DemoUser } from "../../App";
import { acknowledgeAlert as acknowledgeAlertRequest, getAlerts } from "../../services/alertService";
import type { AlertItem } from "../../types/alert";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

type AppShellProps = PropsWithChildren<{
  currentUser: DemoUser;
  onLogout: () => void;
  onRefresh: () => void;
  refreshKey: number;
}>;

const ALERT_POLL_INTERVAL_MS = 30_000;
const ALERT_SNOOZE_MS = 5 * 60 * 1000;

let alertAudioContext: AudioContext | null = null;
let alertAudioContextConstructor: typeof AudioContext | null = null;

function getAudioContextConstructor() {
  const AudioContextConstructor =
    window.AudioContext ??
    (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;

  return AudioContextConstructor ?? null;
}

async function getRunningAlertAudioContext() {
  const AudioContextConstructor = getAudioContextConstructor();
  if (!AudioContextConstructor) {
    return null;
  }

  if (
    !alertAudioContext ||
    alertAudioContext.state === "closed" ||
    alertAudioContextConstructor !== AudioContextConstructor
  ) {
    alertAudioContext = new AudioContextConstructor();
    alertAudioContextConstructor = AudioContextConstructor;
  }

  if (alertAudioContext.state === "suspended") {
    await alertAudioContext.resume();
  }

  return alertAudioContext;
}

function playTone(audioContext: AudioContext, startOffset: number) {
  const oscillator = audioContext.createOscillator();
  const gain = audioContext.createGain();
  const startAt = audioContext.currentTime + startOffset;

  oscillator.type = "sine";
  oscillator.frequency.value = 920;
  gain.gain.setValueAtTime(0.001, startAt);
  gain.gain.exponentialRampToValueAtTime(0.32, startAt + 0.03);
  gain.gain.exponentialRampToValueAtTime(0.001, startAt + 0.2);

  oscillator.connect(gain);
  gain.connect(audioContext.destination);
  oscillator.start(startAt);
  oscillator.stop(startAt + 0.24);
}

function playAlertSound() {
  try {
    void getRunningAlertAudioContext().then((audioContext) => {
      if (!audioContext) {
        return;
      }

      playTone(audioContext, 0);
      playTone(audioContext, 0.28);
      playTone(audioContext, 0.56);
    }).catch(() => undefined);
  } catch {
    // Browser audio policies can reject playback before the first user gesture.
  }
}

function unlockAlertSound() {
  return getRunningAlertAudioContext()
    .then(() => undefined)
    .catch(() => undefined);
}

function AlertNotificationPopup({
  alert,
  onAcknowledge,
  onSnooze
}: {
  alert: AlertItem;
  onAcknowledge: (alertId: string) => void;
  onSnooze: (alertId: string) => void;
}) {
  return (
    <div className="alert-popup-backdrop">
      <section aria-label="Cảnh báo mới" className="alert-popup" role="dialog">
        <div className="alert-popup-head">
          <div>
            <p className="alert-popup-kicker">Cảnh báo mới</p>
            <h2>{alert.title}</h2>
          </div>
          <span className={`alert-popup-severity severity-${alert.severity.toLowerCase()}`}>
            {alert.severity}
          </span>
        </div>
        <p className="alert-popup-message">{alert.message}</p>
        <div className="alert-popup-meta">
          <span>{alert.portCode}</span>
          <span>{alert.zoneName}</span>
          <span>{alert.createdAt}</span>
        </div>
        <div className="alert-popup-actions">
          <button className="secondary-button" onClick={() => onSnooze(alert.alertId)} type="button">
            Báo sau 5 phút
          </button>
          <button className="primary-button" onClick={() => onAcknowledge(alert.alertId)} type="button">
            Xác nhận
          </button>
        </div>
      </section>
    </div>
  );
}

export function AppShell({ children, currentUser, onLogout, onRefresh, refreshKey }: AppShellProps) {
  const [isSidebarOpen, setSidebarOpen] = useState(false);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const [acknowledgedAlertIds, setAcknowledgedAlertIds] = useState<Set<string>>(() => new Set());
  const [snoozedAlertUntil, setSnoozedAlertUntil] = useState<Record<string, number>>({});
  const [now, setNow] = useState(() => Date.now());
  const [unreadAlertCount, setUnreadAlertCount] = useState(0);
  const lastSoundAlertId = useRef<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    const loadAlerts = () => {
      void getAlerts().then((nextAlerts) => {
        if (!isMounted) {
          return;
        }

        setAlerts(nextAlerts);
        setNow(Date.now());
      });
    };

    loadAlerts();
    const interval = window.setInterval(loadAlerts, ALERT_POLL_INTERVAL_MS);

    return () => {
      isMounted = false;
      window.clearInterval(interval);
    };
  }, [refreshKey]);

  const activeAlert = useMemo(
    () =>
      alerts.find(
        (alert) =>
          !alert.read &&
          !acknowledgedAlertIds.has(alert.alertId) &&
          (snoozedAlertUntil[alert.alertId] ?? 0) <= now
      ) ?? null,
    [acknowledgedAlertIds, alerts, now, snoozedAlertUntil]
  );

  useEffect(() => {
    const unlockAudio = () => {
      void unlockAlertSound().then(() => {
        if (!activeAlert) {
          return;
        }

        lastSoundAlertId.current = null;
        playAlertSound();
      });
    };

    window.addEventListener("pointerdown", unlockAudio, { capture: true, once: true });
    window.addEventListener("keydown", unlockAudio, { capture: true, once: true });

    return () => {
      window.removeEventListener("pointerdown", unlockAudio, { capture: true });
      window.removeEventListener("keydown", unlockAudio, { capture: true });
    };
  }, [activeAlert]);

  useEffect(() => {
    setUnreadAlertCount(
      alerts.filter((alert) => !alert.read && !acknowledgedAlertIds.has(alert.alertId)).length
    );
  }, [acknowledgedAlertIds, alerts]);

  useEffect(() => {
    if (!activeAlert || lastSoundAlertId.current === activeAlert.alertId) {
      return;
    }

    lastSoundAlertId.current = activeAlert.alertId;
    playAlertSound();
  }, [activeAlert]);

  useEffect(() => {
    const nextWakeAt = Object.values(snoozedAlertUntil)
      .filter((snoozeUntil) => snoozeUntil > now)
      .sort((first, second) => first - second)[0];

    if (!nextWakeAt) {
      return undefined;
    }

    const timeout = window.setTimeout(() => {
      setNow(Date.now());
    }, Math.max(0, nextWakeAt - now));

    return () => window.clearTimeout(timeout);
  }, [now, snoozedAlertUntil]);

  const acknowledgeAlert = (alertId: string) => {
    setAcknowledgedAlertIds((previous) => new Set(previous).add(alertId));
    void acknowledgeAlertRequest(alertId).catch(() => undefined);
    if (lastSoundAlertId.current === alertId) {
      lastSoundAlertId.current = null;
    }
  };

  const snoozeAlert = (alertId: string) => {
    setSnoozedAlertUntil((previous) => ({
      ...previous,
      [alertId]: Date.now() + ALERT_SNOOZE_MS
    }));
    if (lastSoundAlertId.current === alertId) {
      lastSoundAlertId.current = null;
    }
    setNow(Date.now());
  };

  return (
    <div className="app-shell">
      <Sidebar
        currentUser={currentUser}
        isOpen={isSidebarOpen}
        onClose={() => setSidebarOpen(false)}
        unreadAlertCount={unreadAlertCount}
      />
      {isSidebarOpen ? (
        <button
          aria-label="Đóng menu"
          className="sidebar-backdrop"
          data-testid="sidebar-backdrop"
          onClick={() => setSidebarOpen(false)}
          type="button"
        />
      ) : null}
      <div className="shell-main">
        <Topbar
          currentUser={currentUser}
          onLogout={onLogout}
          onMenuToggle={() => setSidebarOpen((value) => !value)}
          onRefresh={onRefresh}
          unreadAlertCount={unreadAlertCount}
        />
        <main className="content">{children}</main>
      </div>
      {activeAlert ? (
        <AlertNotificationPopup
          alert={activeAlert}
          onAcknowledge={acknowledgeAlert}
          onSnooze={snoozeAlert}
        />
      ) : null}
    </div>
  );
}
