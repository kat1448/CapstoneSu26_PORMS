import { useEffect, useMemo, useRef, useState, type PropsWithChildren } from "react";
import type { DemoUser } from "../../App";
import {
  acknowledgeAlert as acknowledgeAlertRequest,
  getAlerts,
  getAlertSpeechUrl
} from "../../services/alertService";
import type { AlertItem } from "../../types/alert";
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
const POPUP_ALERT_SEVERITIES = new Set(["HIGH", "CRITICAL"]);

type AlertVoiceStatus = "blocked" | "completed" | "idle" | "preparing" | "speaking" | "unsupported";

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

function buildAlertSpeechText(alert: AlertItem) {
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

  const fail = () => {
    if (cancelled || hasFailed) return;
    hasFailed = true;
    callbacks.onError();
  };

  const playWithHtmlAudio = () => {
    fallbackAudio = new Audio(getAlertSpeechUrl(alert.alertId));
    fallbackAudio.preload = "auto";
    fallbackAudio.onplaying = callbacks.onStart;
    fallbackAudio.onended = callbacks.onEnd;
    fallbackAudio.onerror = fail;

    try {
      const playback = fallbackAudio.play();
      if (playback && typeof playback.catch === "function") {
        void playback.catch(fail);
      }
    } catch {
      fail();
    }
  };

  const playWithAudioContext = async () => {
    const audioContext = await getRunningAlertAudioContext();
    if (!audioContext || typeof audioContext.decodeAudioData !== "function") {
      playWithHtmlAudio();
      return;
    }

    const response = await fetch(getAlertSpeechUrl(alert.alertId));
    if (!response.ok) {
      throw new Error(`Speech API returned ${response.status}`);
    }

    const audioBuffer = await audioContext.decodeAudioData(await response.arrayBuffer());
    if (cancelled) return;

    source = audioContext.createBufferSource();
    source.buffer = audioBuffer;
    source.connect(audioContext.destination);
    source.onended = callbacks.onEnd;
    callbacks.onStart();
    source.start();
  };

  if (getAudioContextConstructor()) {
    void playWithAudioContext().catch(() => {
      if (!cancelled && !hasFailed) {
        playWithHtmlAudio();
      }
    });
  } else {
    playWithHtmlAudio();
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
  };
}

function speakAlert(
  alert: AlertItem,
  callbacks: { onEnd: () => void; onError: () => void; onStart: () => void }
) {
  if (!("speechSynthesis" in window) || typeof SpeechSynthesisUtterance === "undefined") {
    return false;
  }

  const speech = new SpeechSynthesisUtterance(buildAlertSpeechText(alert));
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

function AlertNotificationPopup({
  alert,
  onAcknowledge,
  onReplay,
  voiceStatus,
  onSnooze
}: {
  alert: AlertItem;
  onAcknowledge: (alertId: string) => void;
  onReplay: () => void;
  voiceStatus: AlertVoiceStatus;
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
            {riskLabel(alert.severity)}
          </span>
        </div>
        <p className="alert-popup-message">{alert.message}</p>
        <div className={`alert-popup-voice-panel voice-${voiceStatus}`}>
          <button className="alert-popup-replay" onClick={onReplay} type="button">
            <span aria-hidden="true">🔊</span> Nghe cảnh báo
          </button>
        </div>
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
  const [voiceStatus, setVoiceStatus] = useState<AlertVoiceStatus>("idle");
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
          !alert.read &&
          !acknowledgedAlertIds.has(alert.alertId) &&
          (snoozedAlertUntil[alert.alertId] ?? 0) <= now
      ) ?? null,
    [acknowledgedAlertIds, alerts, now, snoozedAlertUntil]
  );

  useEffect(() => {
    const generation = announcementGeneration.current;
    const unlockAudio = () => {
      void unlockAlertSound().then(() => {
        if (!activeAlert || generation !== announcementGeneration.current) {
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
    announceAlert(activeAlert);
  }, [activeAlert]);

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
        <AlertNotificationPopup
          alert={activeAlert}
          onAcknowledge={acknowledgeAlert}
          onReplay={replayAlert}
          onSnooze={snoozeAlert}
          voiceStatus={voiceStatus}
        />
      ) : null}
    </div>
  );
}
