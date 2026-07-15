import { useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import type { DemoUser } from "../../App";
import { Icon } from "../common/Icon";

type TopbarProps = {
  currentUser: DemoUser;
  onLogout: () => void;
  onMenuToggle: () => void;
  onRefresh: () => void;
  unreadAlertCount: number;
};

const pageTitles: Record<string, string> = {
  "/alerts": "Cảnh báo",
  "/change-password": "Đổi mật khẩu",
  "/dashboard": "Dashboard",
  "/forecast-planning": "Dự báo vận hành",
  "/operation-log": "Nhật ký vận hành",
  "/ports": "Cảng & khu vực",
  "/profile": "Thông tin cá nhân",
  "/risk-config": "Cấu hình ngưỡng rủi ro",
  "/simulation": "Chế độ mô phỏng",
  "/simulation-results": "Kết quả mô phỏng",
  "/sop-rules": "Quy tắc SOP",
  "/tasks": "Nhật ký nhiệm vụ",
  "/users": "Người dùng"
};

function formatClock(date: Date) {
  const time = date.toLocaleTimeString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false
  });
  const day = date.toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric"
  });

  return `${time} · ${day}`;
}

const roleLabels: Record<DemoUser["role"], string> = {
  SUPER_ADMIN: "Super Admin",
  ADMIN: "Admin",
  STANDARD_USER: "Standard User"
};

export function Topbar({ currentUser, onLogout, onMenuToggle, onRefresh, unreadAlertCount }: TopbarProps) {
  const location = useLocation();
  const path = location.pathname.startsWith("/ports/")
    ? "/ports"
    : location.pathname.startsWith("/users/")
      ? "/users"
      : location.pathname;
  const [clock, setClock] = useState(() => formatClock(new Date()));
  const [isAccountOpen, setAccountOpen] = useState(false);
  const accountRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const timer = window.setInterval(() => setClock(formatClock(new Date())), 1000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!isAccountOpen) return;

    const closeOnOutsideClick = (event: MouseEvent) => {
      if (!accountRef.current?.contains(event.target as Node)) setAccountOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setAccountOpen(false);
    };

    document.addEventListener("mousedown", closeOnOutsideClick);
    document.addEventListener("keydown", closeOnEscape);
    return () => {
      document.removeEventListener("mousedown", closeOnOutsideClick);
      document.removeEventListener("keydown", closeOnEscape);
    };
  }, [isAccountOpen]);

  return (
    <header className="topbar">
      <button aria-label="Mở menu" className="topbar-icon-button mobile-toggle" onClick={onMenuToggle} type="button"><Icon name="menu" /></button>
      <span className="page-title">{pageTitles[path] ?? "PORMS"}</span>
      <span className="crumb">Cảng Tiên Sa — Đà Nẵng</span>
      <div className="topbar-actions">
        <span className="clock">{clock}</span>
        <Link aria-label={`${unreadAlertCount} cảnh báo chưa đọc`} className="topbar-icon-button top-alert" to="/alerts">
          <Icon name="bell" />{unreadAlertCount > 0 ? <span className="top-alert-count">{unreadAlertCount}</span> : null}
        </Link>
        <button aria-label="Làm mới" className="topbar-refresh-button" onClick={onRefresh} type="button"><Icon name="refresh" /><span>Làm mới</span></button>
        <div className="account-control" ref={accountRef}>
          <button
            aria-expanded={isAccountOpen}
            aria-haspopup="menu"
            aria-label={`Tài khoản ${currentUser.name}`}
            className="topbar-icon-button"
            onClick={() => setAccountOpen((value) => !value)}
            type="button"
          >
            <Icon name="user" />
          </button>
          {isAccountOpen ? (
            <div aria-label="Tài khoản" className="account-menu" role="menu">
              <div className="menu-head">
                <strong>{currentUser.name}</strong>
                <small>{currentUser.email} · {roleLabels[currentUser.role]}</small>
              </div>
              <Link className="menu-item" onClick={() => setAccountOpen(false)} role="menuitem" to="/profile">
                <Icon name="user" /> Thông tin cá nhân
              </Link>
              <Link className="menu-item" onClick={() => setAccountOpen(false)} role="menuitem" to="/change-password">
                <Icon name="lock" /> Đổi mật khẩu
              </Link>
              <button
                className="menu-item menu-item-danger"
                onClick={() => {
                  setAccountOpen(false);
                  onLogout();
                }}
                role="menuitem"
                type="button"
              >
                <Icon name="logout" /> Đăng xuất
              </button>
            </div>
          ) : null}
        </div>
      </div>
    </header>
  );
}
