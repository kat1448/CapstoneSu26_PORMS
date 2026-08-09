import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { Badge } from "../components/common/Badge";
import { deleteUser, getUsers, type UserRecord, type UserStatus } from "../services/userService";

type UsersPageProps = { refreshKey: number };

const PAGE_SIZES = [5, 10, 20];

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

function initials(fullName: string) {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  return (parts.length > 1 ? `${parts[0][0]}${parts.at(-1)?.[0] ?? ""}` : parts[0]?.slice(0, 2) ?? "ND").toUpperCase();
}

function visiblePageNumbers(currentPage: number, totalPages: number) {
  const start = Math.max(1, Math.min(currentPage - 2, totalPages - 4));
  const end = Math.min(totalPages, start + 4);
  return Array.from({ length: Math.max(0, end - start + 1) }, (_, index) => start + index);
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
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

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

  const users = useMemo(() => {
    const normalizedSearch = search.trim().toLocaleLowerCase("vi-VN");
    return allUsers.filter((user) => {
      const searchable = [user.fullName, user.email, user.portName].join(" ").toLocaleLowerCase("vi-VN");
      return (!normalizedSearch || searchable.includes(normalizedSearch))
        && (roleFilter === "ALL" || user.role === roleFilter)
        && (statusFilter === "ALL" || user.status === statusFilter)
        && (portFilter === "ALL" || user.portName === portFilter);
    });
  }, [allUsers, portFilter, roleFilter, search, statusFilter]);

  const totalPages = Math.max(1, Math.ceil(users.length / pageSize));
  const safePage = Math.min(currentPage, totalPages);
  const firstItem = users.length === 0 ? 0 : (safePage - 1) * pageSize + 1;
  const lastItem = Math.min(safePage * pageSize, users.length);
  const pagedUsers = users.slice((safePage - 1) * pageSize, safePage * pageSize);
  const pageNumbers = visiblePageNumbers(safePage, totalPages);

  useEffect(() => { setCurrentPage(1); }, [search, roleFilter, statusFilter, portFilter, pageSize]);
  useEffect(() => { if (currentPage > totalPages) setCurrentPage(totalPages); }, [currentPage, totalPages]);

  async function handleDelete(user: UserRecord) {
    if (user.role === "ADMIN" || !window.confirm(`Xóa người dùng ${user.fullName}?`)) return;
    setError(null);
    setMessage(null);
    try {
      await deleteUser(user.userId);
      setMessage(`Đã xóa tài khoản ${user.fullName}.`);
      await loadUsers();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Không thể xóa người dùng.");
    }
  }

  function resetFilters() {
    setSearch("");
    setRoleFilter("ALL");
    setStatusFilter("ALL");
    setPortFilter("ALL");
  }

  const portNames = Array.from(new Set(allUsers.map((user) => user.portName))).filter((name) => name !== "Tất cả");
  const activeCount = allUsers.filter((user) => user.status === "ACTIVE").length;
  const managerCount = allUsers.filter((user) => user.role === "PORT_MANAGER").length;
  const operatorCount = allUsers.filter((user) => user.role === "OPERATOR").length;

  return (
    <section className="page-grid users-page">
      <div className="section-heading users-heading">
        <div>
          <span className="page-eyebrow">QUẢN TRỊ HỆ THỐNG</span>
          <h2>Người dùng</h2>
          <p>Quản lý tài khoản, vai trò và phạm vi cảng được phân công.</p>
        </div>
        <Link aria-label="Thêm người dùng" className="btn primary" to="/users/new">+ Thêm người dùng</Link>
      </div>

      {error ? (
        <div className="user-feedback is-error" role="alert">
          <div><strong>Không thể tải danh sách người dùng</strong><span>{error}</span></div>
          <button className="btn ghost btn-small" onClick={() => void loadUsers()} type="button">Thử lại</button>
        </div>
      ) : null}
      {message ? <div className="user-feedback is-success" role="status"><strong>Thao tác thành công</strong><span>{message}</span></div> : null}

      <div className="user-stat-grid" aria-label="Tổng quan tài khoản">
        <article><span>Tổng tài khoản</span><strong>{loading ? "—" : allUsers.length}</strong><small>Đang được quản lý</small></article>
        <article><span>Đang hoạt động</span><strong>{loading ? "—" : activeCount}</strong><small>Tài khoản có thể đăng nhập</small></article>
        <article><span>Quản lý cảng</span><strong>{loading ? "—" : managerCount}</strong><small>Phụ trách điều phối tại cảng</small></article>
        <article><span>Nhân viên vận hành</span><strong>{loading ? "—" : operatorCount}</strong><small>Tiếp nhận và xử lý nhiệm vụ</small></article>
      </div>

      <div aria-label="Bộ lọc người dùng" className="card user-filter-card">
        <div className="user-filter-head">
          <div><span className="user-filter-eyebrow">BỘ LỌC TÀI KHOẢN</span><h3>Tìm đúng người dùng cần quản lý</h3><p>Kết quả được cập nhật ngay khi bạn thay đổi điều kiện lọc.</p></div>
          <button className="btn ghost user-reset-button" disabled={!search && roleFilter === "ALL" && statusFilter === "ALL" && portFilter === "ALL"} onClick={resetFilters} type="button">Xóa bộ lọc</button>
        </div>
        <label className="user-search-field"><span>Tìm người dùng</span><input aria-label="Tìm người dùng" className="input" onChange={(event) => setSearch(event.target.value)} placeholder="Nhập tên, email hoặc cảng..." value={search} /></label>
        <label><span>Vai trò</span><select aria-label="Lọc theo vai trò" className="input" onChange={(event) => setRoleFilter(event.target.value)} value={roleFilter}><option value="ALL">Tất cả vai trò</option><option value="ADMIN">Quản trị viên</option><option value="PORT_MANAGER">Quản lý cảng</option><option value="OPERATOR">Nhân viên vận hành</option></select></label>
        <label><span>Cảng phụ trách</span><select aria-label="Lọc theo cảng" className="input" onChange={(event) => setPortFilter(event.target.value)} value={portFilter}><option value="ALL">Tất cả cảng</option>{portNames.map((name) => <option key={name} value={name}>{name}</option>)}</select></label>
        <label><span>Trạng thái</span><select aria-label="Lọc theo trạng thái" className="input" onChange={(event) => setStatusFilter(event.target.value)} value={statusFilter}><option value="ALL">Tất cả trạng thái</option><option value="ACTIVE">Đang hoạt động</option><option value="INACTIVE">Tạm ngưng</option><option value="LOCKED">Đang khóa</option></select></label>
      </div>

      <div className="card user-table-card">
        <div className="user-result-count">
          <div><strong>Danh sách tài khoản</strong><span>{loading ? "Đang tải dữ liệu..." : users.length === 0 ? "Không có tài khoản phù hợp" : `Hiển thị ${firstItem}–${lastItem} trong ${users.length} tài khoản phù hợp`}</span></div>
          <label><span>Số dòng</span><select aria-label="Số người dùng mỗi trang" className="input" onChange={(event) => setPageSize(Number(event.target.value))} value={pageSize}>{PAGE_SIZES.map((size) => <option key={size} value={size}>{size}</option>)}</select></label>
        </div>
        <div className="user-table-scroll">
          <table className="data-table user-data-table">
            <thead><tr><th>Người dùng</th><th>Vai trò</th><th>Cảng phụ trách</th><th>Trạng thái</th><th>Đăng nhập gần nhất</th><th>Thao tác</th></tr></thead>
            <tbody>
              {loading ? <tr><td colSpan={6}><div className="user-empty-state" role="status"><span className="user-loading-ring" /><strong>Đang tải danh sách tài khoản</strong><span>Hệ thống đang lấy dữ liệu người dùng.</span></div></td></tr> : pagedUsers.length === 0 ? <tr><td colSpan={6}><div className="user-empty-state"><span className="user-empty-icon">⌕</span><strong>{error ? "Không thể tải dữ liệu" : "Không tìm thấy tài khoản phù hợp"}</strong><span>{error ? "Bấm Thử lại để tải lại dữ liệu." : "Hãy thay đổi từ khóa hoặc xóa bớt bộ lọc."}</span></div></td></tr> : pagedUsers.map((user) => (
                <tr key={user.userId}>
                  <td><div className="user-identity"><span className={`user-avatar role-${String(user.role).toLowerCase()}`}>{initials(user.fullName)}</span><div><strong>{user.fullName}</strong><small>{user.email}</small></div></div></td>
                  <td><strong className="user-role-label">{roleLabel(user.role)}</strong></td>
                  <td><span className="user-port-label">{user.portName}</span></td>
                  <td><Badge tone={statusTone(user.status)}>{statusLabel(user.status)}</Badge></td>
                  <td><span className="user-last-login">{user.lastLoginLabel}</span></td>
                  <td><div className="table-actions"><Link aria-label={`Sửa ${user.fullName}`} className="btn ghost btn-small" to={`/users/${user.userId}/edit`}>Sửa</Link>{user.role !== "ADMIN" ? <button aria-label={`Xóa ${user.fullName}`} className="btn danger btn-small" onClick={() => void handleDelete(user)} type="button">Xóa</button> : null}</div></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!loading && users.length > 0 ? (
          <nav aria-label="Phân trang người dùng" className="user-pagination">
            <span>Trang <strong>{safePage}</strong> / {totalPages}</span>
            <div>
              <button aria-label="Trang đầu" disabled={safePage === 1} onClick={() => setCurrentPage(1)} type="button">«</button>
              <button aria-label="Trang trước" disabled={safePage === 1} onClick={() => setCurrentPage((page) => Math.max(1, page - 1))} type="button">‹</button>
              {pageNumbers.map((page) => <button aria-current={safePage === page ? "page" : undefined} className={safePage === page ? "is-active" : ""} key={page} onClick={() => setCurrentPage(page)} type="button">{page}</button>)}
              <button aria-label="Trang sau" disabled={safePage === totalPages} onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))} type="button">›</button>
              <button aria-label="Trang cuối" disabled={safePage === totalPages} onClick={() => setCurrentPage(totalPages)} type="button">»</button>
            </div>
          </nav>
        ) : null}
      </div>
    </section>
  );
}
