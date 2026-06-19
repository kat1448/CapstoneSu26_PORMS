import { useEffect, useState } from "react";
import { Badge } from "../components/common/Badge";
import { getUsers, type UserRecord } from "../services/userService";

type UsersPageProps = {
  refreshKey: number;
};

export function UsersPage({ refreshKey }: UsersPageProps) {
  const [users, setUsers] = useState<UserRecord[]>([]);

  useEffect(() => {
    void getUsers().then(setUsers);
  }, [refreshKey]);

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Users</h2>
          <p>Quản lý tài khoản và phân quyền người dùng</p>
        </div>
      </div>
      <div className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Họ tên</th>
              <th>Email</th>
              <th>Role</th>
              <th>Port</th>
              <th>Trạng thái</th>
              <th>Đăng nhập gần nhất</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.userId}>
                <td>{user.fullName}</td>
                <td>{user.email}</td>
                <td>{user.role}</td>
                <td>{user.portName}</td>
                <td>
                  <Badge tone={user.status === "LOCKED" ? "danger" : "success"}>{user.status}</Badge>
                </td>
                <td>{user.lastLoginLabel}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
