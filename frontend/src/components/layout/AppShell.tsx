import { useEffect, useMemo, useRef, useState, type PropsWithChildren } from "react";
import type { DemoUser } from "../../App";
import {
  acknowledgeAlert as acknowledgeAlertRequest,
  getAlertSpeechAudio,
  getAlerts,
} from "../../services/alertService";
import type { AlertItem } from "../../types/alert";
import {
  getNotificationPreferences,
  NOTIFICATION_PREFERENCES_CHANGED,
  severityMeetsPreference,
  type NotificationPreferences
} from "../../services/notificationPreferenceService";
import { riskLabel } from "../../utils/displayLabels";
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
const LEGACY_ALERT_ACTIVE_WINDOW_MS = 2 * 60 * 60 * 1000;
const POPUP_ALERT_SEVERITIES = new Set(["HIGH", "CRITICAL"]);

type AlertVoiceStatus = "blocked" | "completed" | "idle" | "preparing" | "speaking" | "unsupported";

function isAlertActive(alert: AlertItem, now: number) {
  const explicitExpiration = Date.parse(alert.expiresAt ?? "");
  if (Number.isFinite(explicitExpiration)) {
    return explicitExpiration > now;
  }

  // Cảnh báo cũ có thể chưa có expiresAt, nên chỉ phát trong 2 giờ kể từ lúc tạo.
  const createdAt = Date.parse(alert.createdAtIso ?? alert.createdAt);
  return Number.isFinite(createdAt) && createdAt + LEGACY_ALERT_ACTIVE_WINDOW_MS > now;
}

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

function playAlertSound(shouldPlay: () => boolean = () => true) {
  try {
    void getRunningAlertAudioContext().then((audioContext) => {
      if (!audioContext || !shouldPlay()) {
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

function buildFriendlyAlertSpeechText(alert: AlertItem) {
  return `Chú ý. ${alert.title}. ${alert.message}`
    .replace(/\bCRITICAL\b/g, "rất cao")
    .replace(/\bHIGH\b/g, "cao")
    .replace(/\bSOP\b/g, "quy trình ứng phó")
    .replace(/\bm\/s\b/g, "mét trên giây")
    .replace(/mm\/giờ/g, "mi li mét mỗi giờ")
    .replace(/\bkm\b/g, "ki lô mét")
    .replace(/(\d)\.(\d)/g, "$1 phẩy $2");
}

function playOnlineAlertSpeech(
  alert: AlertItem,
  callbacks: { onEnd: () => void; onError: () => void; onStart: () => void }
) {
  if (typeof Audio === "undefined") {
    return null;
  }

  let cancelled = false;
  let hasFailed = false;
  let source: AudioBufferSourceNode | null = null;
  let fallbackAudio: HTMLAudioElement | null = null;
  let fallbackAudioUrl: string | null = null;

  const fail = () => {
    if (cancelled || hasFailed) return;
    hasFailed = true;
    callbacks.onError();
  };

  const fallbackToBrowserSpeech = () => {
    if (cancelled || hasFailed) return;
    const started = speakAlert(alert, callbacks);
    if (!started) fail();
  };

  const playWithHtmlAudio = async () => {
    const speechBlob = await getAlertSpeechAudio(alert.alertId);
    if (cancelled) return;
    fallbackAudioUrl = URL.createObjectURL(speechBlob);
    fallbackAudio = new Audio(fallbackAudioUrl);
    fallbackAudio.preload = "auto";
    fallbackAudio.onplaying = callbacks.onStart;
    fallbackAudio.onended = callbacks.onEnd;
    fallbackAudio.onerror = fallbackToBrowserSpeech;

    try {
      const playback = fallbackAudio.play();
      if (playback && typeof playback.catch === "function") {
        void playback.catch(fallbackToBrowserSpeech);
      }
    } catch {
      fallbackToBrowserSpeech();
    }
  };

  const playWithAudioContext = async () => {
    const audioContext = await getRunningAlertAudioContext();
    if (!audioContext || typeof audioContext.decodeAudioData !== "function") {
      await playWithHtmlAudio();
      return;
    }

    const speechBlob = await getAlertSpeechAudio(alert.alertId);
    const audioBuffer = await audioContext.decodeAudioData(await speechBlob.arrayBuffer());
    if (cancelled) return;

    source = audioContext.createBufferSource();
    source.buffer = audioBuffer;
    source.connect(audioContext.destination);
    source.onended = callbacks.onEnd;
    callbacks.onStart();
    source.start();
  };

  if (getAudioContextConstructor()) {
    void playWithAudioContext().catch(fallbackToBrowserSpeech);
  } else {
    void playWithHtmlAudio().catch(fallbackToBrowserSpeech);
  }

  return () => {
    cancelled = true;
    try {
      source?.stop();
      source?.disconnect();
    } catch {
      // The source may already have finished when the popup is dismissed.
    }
    fallbackAudio?.pause();
    fallbackAudio?.removeAttribute("src");
    fallbackAudio?.load();
    if (fallbackAudioUrl) {
      URL.revokeObjectURL(fallbackAudioUrl);
      fallbackAudioUrl = null;
    }
  };
}

function speakAlert(
  alert: AlertItem,
  callbacks: { onEnd: () => void; onError: () => void; onStart: () => void }
) {
  if (!("speechSynthesis" in window) || typeof SpeechSynthesisUtterance === "undefined") {
    return false;
  }

  const speech = new SpeechSynthesisUtterance(buildFriendlyAlertSpeechText(alert));
  const synthesizer = window.speechSynthesis;
  const voices = synthesizer.getVoices();
  const vietnameseVoice = voices.find((voice) => voice.lang.toLocaleLowerCase().startsWith("vi"));
  const fallbackVoice = voices.find((voice) => voice.default) ?? voices[0];

  speech.lang = "vi-VN";
  speech.rate = 0.92;
  speech.pitch = 1;
  speech.volume = 1;
  speech.onstart = callbacks.onStart;
  speech.onend = callbacks.onEnd;
  speech.onerror = (event) => {
    if (event.error !== "canceled" && event.error !== "interrupted") {
      callbacks.onError();
    }
  };
  if (vietnameseVoice ?? fallbackVoice) {
    speech.voice = vietnameseVoice ?? fallbackVoice;
  }

  try {
    synthesizer.cancel();
    synthesizer.resume();
    synthesizer.speak(speech);
    return true;
  } catch {
    callbacks.onError();
    return false;
  }
}

function cancelAlertSpeech() {
  if ("speechSynthesis" in window) {
    window.speechSynthesis.cancel();
  }
}

function AlertNotificationPopupV2({
  alert,
  onAcknowledge,
  onReplay,
  onSnooze,
  voiceStatus,
  voiceEnabled
}: {
  alert: AlertItem;
  onAcknowledge: (alertId: string) => void;
  onReplay: () => void;
  onSnooze: (alertId: string) => void;
  voiceStatus: AlertVoiceStatus;
  voiceEnabled: boolean;
}) {
  const action = alert.severity === "CRITICAL"
    ? "Tạm dừng hoạt động tại khu vực và thực hiện ngay quy trình ứng phó khẩn cấp."
    : "Hạn chế hoạt động tại khu vực và triển khai các nhiệm vụ ứng phó được giao.";

  return (
    <div className="alert-popup-backdrop">
      <section aria-label="Cảnh báo mới" className={`alert-popup alert-popup-v2 severity-${alert.severity.toLowerCase()}`} data-voice-status={voiceStatus} role="dialog">
        <div className="alert-popup-accent" />
        <div className="alert-popup-head">
          <div className="alert-popup-heading-group">
            <span className="alert-popup-icon" aria-hidden="true">!</span>
            <div><p className="alert-popup-kicker">CẢNH BÁO MỚI</p><h2>{alert.title}</h2></div>
          </div>
          <span className={`alert-popup-severity severity-${alert.severity.toLowerCase()}`}>{riskLabel(alert.severity)}</span>
        </div>
        <div className="alert-popup-location"><strong>{alert.portName}</strong><span>{alert.zoneName}</span><time>{alert.createdAt}</time></div>
        <p className="alert-popup-message">{alert.message}</p>
        <div className="alert-popup-recommendation"><span aria-hidden="true">!</span><div><strong>Việc cần thực hiện</strong><p>{action}</p></div></div>
        <div className="alert-popup-footer">
          {voiceEnabled ? <button className="alert-popup-replay" onClick={onReplay} type="button"><span aria-hidden="true">🔊</span> Nghe cảnh báo</button> : <span className="alert-voice-muted">Giọng đọc đang tắt</span>}
          <div className="alert-popup-actions"><button aria-label="Báo sau 5 phút" className="secondary-button" onClick={() => onSnooze(alert.alertId)} type="button">Nhắc lại sau 5 phút</button><button aria-label="Xác nhận" className="primary-button" onClick={() => onAcknowledge(alert.alertId)} type="button">Xác nhận tiếp nhận</button></div>
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
  const [voiceStatus, setVoiceStatus] = useState<AlertVoiceStatus>("idle");
  const userPreferenceKey = currentUser.id ?? currentUser.email;
  const [notificationPreferences, setNotificationPreferences] = useState<NotificationPreferences>(
    () => getNotificationPreferences(userPreferenceKey)
  );
  const lastSoundAlertId = useRef<string | null>(null);
  const speechTimer = useRef<number | null>(null);
  const speechStartGuardTimer = useRef<number | null>(null);
  const stopOnlineSpeech = useRef<(() => void) | null>(null);
  const announcementGeneration = useRef(0);

  const stopAlertAnnouncement = () => {
    if (speechTimer.current !== null) {
      window.clearTimeout(speechTimer.current);
      speechTimer.current = null;
    }
    if (speechStartGuardTimer.current !== null) {
      window.clearTimeout(speechStartGuardTimer.current);
      speechStartGuardTimer.current = null;
    }
    stopOnlineSpeech.current?.();
    stopOnlineSpeech.current = null;
    cancelAlertSpeech();
  };

  const announceAlert = (alert: AlertItem, immediate = false) => {
    stopAlertAnnouncement();
    const generation = announcementGeneration.current;
    setVoiceStatus("preparing");
    if (!immediate) {
      playAlertSound(() => generation === announcementGeneration.current);
    }

    const startSpeech = () => {
      speechTimer.current = null;
      if (generation !== announcementGeneration.current) {
        return;
      }

      const callbacks = {
        onEnd: () => {
          if (generation === announcementGeneration.current) setVoiceStatus("completed");
        },
        onError: () => {
          if (generation === announcementGeneration.current) setVoiceStatus("blocked");
        },
        onStart: () => {
          if (speechStartGuardTimer.current !== null) {
            window.clearTimeout(speechStartGuardTimer.current);
            speechStartGuardTimer.current = null;
          }
          if (generation === announcementGeneration.current) setVoiceStatus("speaking");
        }
      };
      const stopPlayback = playOnlineAlertSpeech(alert, callbacks);
      stopOnlineSpeech.current = stopPlayback;

      if (!stopPlayback) {
        const started = speakAlert(alert, callbacks);
        if (started) return;
        setVoiceStatus("unsupported");
        return;
      }

      speechStartGuardTimer.current = window.setTimeout(() => {
        speechStartGuardTimer.current = null;
        if (generation === announcementGeneration.current) {
          setVoiceStatus((current) => current === "preparing" ? "blocked" : current);
        }
      }, 1800);
    };

    if (immediate) {
      startSpeech();
    } else {
      speechTimer.current = window.setTimeout(startSpeech, 850);
    }
  };

  useEffect(() => {
    setNotificationPreferences(getNotificationPreferences(userPreferenceKey));
    const handlePreferenceChange = (event: Event) => {
      const changed = event as CustomEvent<NotificationPreferences>;
      setNotificationPreferences(changed.detail ?? getNotificationPreferences(userPreferenceKey));
    };
    window.addEventListener(NOTIFICATION_PREFERENCES_CHANGED, handlePreferenceChange);
    return () => window.removeEventListener(NOTIFICATION_PREFERENCES_CHANGED, handlePreferenceChange);
  }, [userPreferenceKey]);

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
          POPUP_ALERT_SEVERITIES.has(alert.severity) &&
          isAlertActive(alert, now) &&
          notificationPreferences.inAppEnabled &&
          severityMeetsPreference(alert.severity, notificationPreferences.minimumSeverity) &&
          !alert.read &&
          !acknowledgedAlertIds.has(alert.alertId) &&
          (snoozedAlertUntil[alert.alertId] ?? 0) <= now
      ) ?? null,
    [acknowledgedAlertIds, alerts, notificationPreferences, now, snoozedAlertUntil]
  );

  useEffect(() => {
    const generation = announcementGeneration.current;
    const unlockAudio = () => {
      void unlockAlertSound().then(() => {
        if (!activeAlert || !notificationPreferences.voiceEnabled || generation !== announcementGeneration.current) {
          return;
        }

        lastSoundAlertId.current = null;
        announceAlert(activeAlert);
      });
    };

    window.addEventListener("pointerdown", unlockAudio, { capture: true, once: true });
    window.addEventListener("keydown", unlockAudio, { capture: true, once: true });

    return () => {
      window.removeEventListener("pointerdown", unlockAudio, { capture: true });
      window.removeEventListener("keydown", unlockAudio, { capture: true });
    };
  }, [activeAlert, notificationPreferences.voiceEnabled]);

  useEffect(() => {
    setUnreadAlertCount(
      alerts.filter(
        (alert) =>
          isAlertActive(alert, now) &&
          !alert.read &&
          !acknowledgedAlertIds.has(alert.alertId)
      ).length
    );
  }, [acknowledgedAlertIds, alerts, now]);

  useEffect(() => {
    if (!activeAlert || lastSoundAlertId.current === activeAlert.alertId) {
      return;
    }

    lastSoundAlertId.current = activeAlert.alertId;
    if (notificationPreferences.voiceEnabled) announceAlert(activeAlert);
  }, [activeAlert, notificationPreferences.voiceEnabled]);

  useEffect(() => () => stopAlertAnnouncement(), []);

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
    announcementGeneration.current += 1;
    stopAlertAnnouncement();
    setAcknowledgedAlertIds((previous) => new Set(previous).add(alertId));
    void acknowledgeAlertRequest(alertId).catch(() => undefined);
    if (lastSoundAlertId.current === alertId) {
      lastSoundAlertId.current = null;
    }
  };

  const snoozeAlert = (alertId: string) => {
    announcementGeneration.current += 1;
    stopAlertAnnouncement();
    setSnoozedAlertUntil((previous) => ({
      ...previous,
      [alertId]: Date.now() + ALERT_SNOOZE_MS
    }));
    if (lastSoundAlertId.current === alertId) {
      lastSoundAlertId.current = null;
    }
    setNow(Date.now());
  };

  const replayAlert = () => {
    if (!activeAlert) return;
    if (!notificationPreferences.voiceEnabled) return;
    announcementGeneration.current += 1;
    announceAlert(activeAlert, true);
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
        <AlertNotificationPopupV2
          alert={activeAlert}
          onAcknowledge={acknowledgeAlert}
          onReplay={replayAlert}
          onSnooze={snoozeAlert}
          voiceStatus={voiceStatus}
          voiceEnabled={notificationPreferences.voiceEnabled}
        />
      ) : null}
    </div>
  );
}
