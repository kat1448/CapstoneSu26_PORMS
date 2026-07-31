import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { deleteUser, getUsers, type UserRecord, type UserStatus } from "../services/userService";

type UsersPageProps = { refreshKey: number };

function statusTone(status: UserStatus) {
  if (status === "LOCKED") return "danger";
  if (status === "INACTIVE") return "muted";
  return "success";
}

function roleLabel(role: UserRecord["role"]) {
  if (role === "ADMIN") return "Quản trị viên";
  if (role === "PORT_MANAGER") return "Quản lý cảng";
  return "Nhân viên vận hành";
}

function statusLabel(status: UserStatus) {
  if (status === "LOCKED") return "Đang khóa";
  if (status === "INACTIVE") return "Tạm ngưng";
  return "Đang hoạt động";
}

export function UsersPage({ refreshKey }: UsersPageProps) {
  const [allUsers, setAllUsers] = useState<UserRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [roleFilter, setRoleFilter] = useState("ALL");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [portFilter, setPortFilter] = useState("ALL");

  async function loadUsers() {
    setLoading(true);
    setError(null);
    try {
      setAllUsers(await getUsers());
    } catch (caught) {
      setAllUsers([]);
      setError(caught instanceof Error ? caught.message : "Không thể tải danh sách tài khoản.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void loadUsers(); }, [refreshKey]);

  const normalizedSearch = search.trim().toLocaleLowerCase("vi-VN");
  const users = allUsers.filter((user) => {
    const searchable = [user.fullName, user.email, user.portName].join(" ").toLocaleLowerCase("vi-VN");
    return (!normalizedSearch || searchable.includes(normalizedSearch))
      && (roleFilter === "ALL" || user.role === roleFilter)
      && (statusFilter === "ALL" || user.status === statusFilter)
      && (portFilter === "ALL" || user.portName === portFilter);
  });

  async function handleDelete(user: UserRecord) {
    if (user.role === "ADMIN" || !window.confirm(`Xóa người dùng ${user.fullName}?`)) return;
    setError(null); setMessage(null);
    try {
      await deleteUser(user.userId);
      setMessage("Đã xóa người dùng.");
      await loadUsers();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể xóa người dùng.");
    }
  }

  function resetFilters() {
    setSearch(""); setRoleFilter("ALL"); setStatusFilter("ALL"); setPortFilter("ALL");
  }

  const portNames = Array.from(new Set(allUsers.map((user) => user.portName))).filter((name) => name !== "Tất cả");

  return (
    <section className="page-grid">
      <div className="section-heading">
        <div><h2>Người dùng</h2><p>Quản lý tài khoản và phân quyền người dùng</p></div>
        <Link className="btn primary" to="/users/new">Thêm người dùng</Link>
      </div>

      {error ? <div className="form-error" role="alert">{error}</div> : null}
      {message ? <div className="form-success" role="status">{message}</div> : null}

      <div aria-label="Bộ lọc người dùng" className="card user-filter-card">
        <div className="user-filter-head">
          <div><span className="user-filter-eyebrow">BỘ LỌC TÀI KHOẢN</span><h3>Tìm và lọc người dùng</h3><p>Tìm nhanh theo tên, email, cảng hoặc khu vực phụ trách.</p></div>
          <span className="user-filter-hint">Lọc tức thì</span>
        </div>
        <label className="user-search-field"><span>Tìm người dùng</span><input aria-label="Tìm người dùng" className="input" onChange={(event) => setSearch(event.target.value)} placeholder="Nhập tên, email, cảng hoặc khu vực..." value={search} /></label>
        <label><span>Vai trò</span><select aria-label="Lọc theo vai trò" className="input" onChange={(event) => setRoleFilter(event.target.value)} value={roleFilter}><option value="ALL">Tất cả vai trò</option><option value="ADMIN">Quản trị viên</option><option value="PORT_MANAGER">Quản lý cảng</option><option value="OPERATOR">Nhân viên vận hành</option></select></label>
        <label><span>Cảng/khu vực</span><select aria-label="Lọc theo cảng" className="input" onChange={(event) => setPortFilter(event.target.value)} value={portFilter}><option value="ALL">Tất cả cảng</option>{portNames.map((name) => <option key={name} value={name}>{name}</option>)}</select></label>
        <label><span>Trạng thái</span><select aria-label="Lọc theo trạng thái" className="input" onChange={(event) => setStatusFilter(event.target.value)} value={statusFilter}><option value="ALL">Tất cả trạng thái</option><option value="ACTIVE">Đang hoạt động</option><option value="INACTIVE">Tạm ngưng</option><option value="LOCKED">Đang khóa</option></select></label>
        <button className="btn ghost user-reset-button" onClick={resetFilters} type="button">Xóa bộ lọc</button>
      </div>

      <div className="card table-card">
        <div className="user-result-count"><span>Đang hiển thị <strong>{loading ? "—" : users.length}</strong> tài khoản phù hợp</span>{!loading && !error ? <span className="user-result-hint">Có thể kết hợp nhiều bộ lọc</span> : null}</div>
        <table className="data-table"><thead><tr><th>Họ tên</th><th>Email</th><th>Vai trò</th><th>Cảng phụ trách</th><th>Trạng thái</th><th>Đăng nhập gần nhất</th><th>Thao tác</th></tr></thead>
          <tbody>{loading ? <tr><td colSpan={7}><div className="user-empty-state" role="status"><strong>Đang tải danh sách tài khoản</strong><span>Vui lòng chờ trong giây lát.</span></div></td></tr> : users.length === 0 ? <tr><td colSpan={7}><div className="user-empty-state"><strong>{error ? "Không thể tải dữ liệu" : "Không tìm thấy tài khoản phù hợp"}</strong><span>{error ? "Hãy kiểm tra quyền truy cập hoặc thử làm mới trang." : "Thử đổi từ khóa hoặc xóa bớt bộ lọc để xem thêm tài khoản."}</span></div></td></tr> : users.map((user) => <tr key={user.userId}><td>{user.fullName}</td><td>{user.email}</td><td>{roleLabel(user.role)}</td><td>{user.portName}</td><td><Badge tone={statusTone(user.status)}>{statusLabel(user.status)}</Badge></td><td>{user.lastLoginLabel}</td><td><div className="table-actions"><Link aria-label={`Sửa ${user.fullName}`} className="btn ghost btn-small" to={`/users/${user.userId}/edit`}>Sửa</Link>{user.role !== "ADMIN" ? <button aria-label={`Xóa ${user.fullName}`} className="btn danger btn-small" onClick={() => void handleDelete(user)} type="button">Xóa</button> : null}</div></td></tr>)}</tbody>
        </table>
      </div>
    </section>
  );
}
