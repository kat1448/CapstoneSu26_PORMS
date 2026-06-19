import { NavLink } from "react-router-dom";
import type { DemoUser } from "../../App";
import { getNavigationForRole } from "../../navigation/navigation";
import { Icon } from "../common/Icon";

type SidebarProps = {
  currentUser: DemoUser;
  isOpen: boolean;
  onClose: () => void;
  unreadAlertCount: number;
};

const roleLabels = {
  ADMIN: "Quản trị hệ thống",
  PORT_MANAGER: "Quản lý cảng",
  OPERATOR: "Nhân viên vận hành"
} as const;

export function Sidebar({ currentUser, isOpen, onClose, unreadAlertCount }: SidebarProps) {
  return (
    <nav aria-label="Điều hướng chính" className={`sidebar${isOpen ? " open" : ""}`}>
      <div className="brand">
        <div className="brand-mark">P</div>
        <div><strong>PORMS</strong><small>Cảng Tiên Sa · DNTSA</small></div>
      </div>
      <div className="sidebar-scroll">
        {getNavigationForRole(currentUser.role).map((group) => (
          <section className="nav-group" key={group.label}>
            <p className="nav-label">{group.label}</p>
            {group.items.map((item) => (
              <NavLink
                className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}
                key={item.path}
                onClick={onClose}
                to={item.path}
              >
                <Icon name={item.icon} />
                <span>{item.label}</span>
                {item.path === "/alerts" && unreadAlertCount > 0 ? (
                  <span aria-label={`${unreadAlertCount} cảnh báo chưa đọc`} className="count">{unreadAlertCount}</span>
                ) : null}
              </NavLink>
            ))}
          </section>
        ))}
      </div>
      <NavLink className="side-user" onClick={onClose} to="/profile">
        <span className="avatar">{currentUser.initials}</span>
        <span><strong>{currentUser.name}</strong><small>{roleLabels[currentUser.role]}</small></span>
      </NavLink>
    </nav>
  );
}
