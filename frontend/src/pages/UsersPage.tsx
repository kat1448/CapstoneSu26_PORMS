import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { deleteUser, getUsers, type UserRecord, type UserStatus } from "../services/userService";

type UsersPageProps = {
  refreshKey: number;
};

function statusTone(status: UserStatus) {
  if (status === "LOCKED") return "danger";
  if (status === "INACTIVE") return "muted";
  return "success";
}

export function UsersPage({ refreshKey }: UsersPageProps) {
  const [users, setUsers] = useState<UserRecord[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function loadUsers() {
    setUsers(await getUsers());
  }

  useEffect(() => {
    void loadUsers();
  }, [refreshKey]);

  async function handleDelete(user: UserRecord) {
    if (!window.confirm(`Xóa người dùng ${user.fullName}?`)) {
      return;
    }

    setError(null);
    setMessage(null);

    try {
      await deleteUser(user.userId);
      setMessage("Đã xoá người dùng.");
      await loadUsers();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể xoá người dùng.");
    }
  }

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div>
          <h2>Người dùng</h2>
          <p>Quản lý tài khoản và phân quyền người dùng</p>
        </div>
        <Link className="btn primary" to="/users/new">Thêm người dùng</Link>
      </div>

      {error ? <div className="form-error" role="alert">{error}</div> : null}
      {message ? <div className="form-success" role="status">{message}</div> : null}

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
              <th>Thao tác</th>
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
                  <Badge tone={statusTone(user.status)}>{user.status}</Badge>
                </td>
                <td>{user.lastLoginLabel}</td>
                <td>
                  <div className="table-actions">
                    <Link
                      aria-label={`Sửa ${user.fullName}`}
                      className="btn ghost btn-small"
                      to={`/users/${user.userId}/edit`}
                    >
                      Sửa
                    </Link>
                    <button
                      aria-label={`Xóa ${user.fullName}`}
                      className="btn danger btn-small"
                      onClick={() => void handleDelete(user)}
                      type="button"
                    >
                      Xoá
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
