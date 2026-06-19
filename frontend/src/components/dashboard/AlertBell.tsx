import { Link } from "react-router-dom";

type AlertBellProps = {
  unreadCount: number;
};

export function AlertBell({ unreadCount }: AlertBellProps) {
  return (
    <Link aria-label="Cảnh báo" className="alert-bell" to="/alerts">
      <span className="alert-bell-icon">!</span>
      {unreadCount > 0 ? <span className="alert-bell-count">{unreadCount}</span> : null}
    </Link>
  );
}
