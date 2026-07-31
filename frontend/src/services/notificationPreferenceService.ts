export type NotificationPreferences = {
  inAppEnabled: boolean;
  voiceEnabled: boolean;
  minimumSeverity: "HIGH" | "CRITICAL";
};

export const NOTIFICATION_PREFERENCES_CHANGED = "porms:notification-preferences-changed";

const defaultPreferences: NotificationPreferences = {
  inAppEnabled: true,
  voiceEnabled: true,
  minimumSeverity: "HIGH"
};

function storageKey(userKey: string) {
  return `porms.notification.preferences.${userKey.toLowerCase()}`;
}

export function getNotificationPreferences(userKey: string): NotificationPreferences {
  try {
    const raw = localStorage.getItem(storageKey(userKey));
    if (!raw) return defaultPreferences;
    return { ...defaultPreferences, ...JSON.parse(raw) } as NotificationPreferences;
  } catch {
    return defaultPreferences;
  }
}

export function saveNotificationPreferences(userKey: string, preferences: NotificationPreferences) {
  localStorage.setItem(storageKey(userKey), JSON.stringify(preferences));
  window.dispatchEvent(new CustomEvent(NOTIFICATION_PREFERENCES_CHANGED, { detail: preferences }));
}

export function severityMeetsPreference(severity: string, minimum: NotificationPreferences["minimumSeverity"]) {
  if (minimum === "CRITICAL") return severity === "CRITICAL";
  return severity === "HIGH" || severity === "CRITICAL";
}
