import { useState } from "react";
import type { DemoUser } from "../App";
import {
  getNotificationPreferences,
  saveNotificationPreferences,
  type NotificationPreferences
} from "../services/notificationPreferenceService";

type NotificationSettingsPageProps = { currentUser: DemoUser };

export function NotificationSettingsPage({ currentUser }: NotificationSettingsPageProps) {
  const userKey = currentUser.id ?? currentUser.email;
  const [preferences, setPreferences] = useState<NotificationPreferences>(() => getNotificationPreferences(userKey));
  const [saved, setSaved] = useState(false);

  function update<K extends keyof NotificationPreferences>(key: K, value: NotificationPreferences[K]) {
    setSaved(false);
    setPreferences((current) => ({ ...current, [key]: value }));
  }

  function save() {
    saveNotificationPreferences(userKey, preferences);
    setSaved(true);
  }

  return (
    <section className="page-grid notification-settings-page">
      <div className="section-heading notification-settings-heading"><div><span className="page-eyebrow">TÙY CHỌN CÁ NHÂN</span><h2>Cài đặt thông báo</h2><p>Chọn cách PORMS nhắc bạn khi cảng xuất hiện tình huống cần chú ý.</p></div></div>
      <div className="notification-settings-layout">
        <section className="card notification-preference-card">
          <div className="preference-card-head"><div className="preference-icon">!</div><div><h3>Cảnh báo trên website</h3><p>Hiển thị popup khi có cảnh báo mới thuộc cảng bạn phụ trách.</p></div><label className="switch-control"><input checked={preferences.inAppEnabled} onChange={(event) => update("inAppEnabled", event.target.checked)} type="checkbox" /><span /></label></div>
          <div className="preference-divider" />
          <div className="preference-card-head"><div className="preference-icon voice-icon">◖</div><div><h3>Đọc cảnh báo bằng giọng nói</h3><p>Phát âm báo hiệu và đọc nội dung cảnh báo bằng tiếng Việt.</p></div><label className="switch-control"><input checked={preferences.voiceEnabled} disabled={!preferences.inAppEnabled} onChange={(event) => update("voiceEnabled", event.target.checked)} type="checkbox" /><span /></label></div>
        </section>
        <aside className="card notification-scope-card"><span className="page-eyebrow">PHẠM VI ÁP DỤNG</span><h3>{currentUser.role === "ADMIN" ? "Toàn bộ hệ thống" : currentUser.portName}</h3><p>{currentUser.role === "ADMIN" ? "Bạn nhận cảnh báo từ tất cả cảng đang hoạt động." : "Bạn chỉ nhận cảnh báo phát sinh tại cảng được phân công."}</p><div className="scope-role-pill">{currentUser.role === "ADMIN" ? "Quản trị hệ thống" : currentUser.role === "PORT_MANAGER" ? "Quản lý cảng" : "Nhân viên vận hành"}</div></aside>
      </div>
      <section className="card severity-preference-card"><div><span className="page-eyebrow">MỨC CẢNH BÁO TỰ ĐỘNG</span><h3>Khi nào PORMS cần bật popup và giọng đọc?</h3><p>Cảnh báo thấp hơn vẫn được lưu trong Trung tâm cảnh báo để bạn theo dõi khi cần.</p></div><div className="severity-choice-grid"><label className={preferences.minimumSeverity === "HIGH" ? "selected" : ""}><input checked={preferences.minimumSeverity === "HIGH"} name="minimumSeverity" onChange={() => update("minimumSeverity", "HIGH")} type="radio" /><span className="severity-choice-icon high">!</span><div><strong>Từ mức Cao</strong><small>Thông báo cho cả mức Cao và Rất cao</small></div></label><label className={preferences.minimumSeverity === "CRITICAL" ? "selected" : ""}><input checked={preferences.minimumSeverity === "CRITICAL"} name="minimumSeverity" onChange={() => update("minimumSeverity", "CRITICAL")} type="radio" /><span className="severity-choice-icon critical">!</span><div><strong>Chỉ mức Rất cao</strong><small>Chỉ thông báo khi cần hành động ngay</small></div></label></div></section>
      <div className="notification-save-bar"><div>{saved ? <span className="save-success">✓ Đã lưu tùy chọn thông báo</span> : <span>Các tùy chọn được lưu riêng cho tài khoản này trên thiết bị hiện tại.</span>}</div><button className="button button-primary" onClick={save} type="button">Lưu cài đặt</button></div>
    </section>
  );
}
